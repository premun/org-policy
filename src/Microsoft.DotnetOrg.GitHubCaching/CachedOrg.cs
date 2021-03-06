﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.Ospo;

using Octokit.GraphQL;

namespace Microsoft.DotnetOrg.GitHubCaching
{
    public sealed class CachedOrg
    {
        public static int CurrentVersion = 8;

        public int Version { get; set; }
        public string Name { get; set; }
        public List<CachedTeam> Teams { get; set; } = new List<CachedTeam>();
        public List<CachedRepo> Repos { get; set; } = new List<CachedRepo>();
        public List<CachedUserAccess> Collaborators { get; set; } = new List<CachedUserAccess>();
        public List<CachedTeamAccess> TeamAccess { get; set; } = new List<CachedTeamAccess>();
        public List<CachedUser> Users { get; set; } = new List<CachedUser>();

        public void Initialize()
        {
            if (Version != CurrentVersion)
                return;

            var teamById = Teams.ToDictionary(t => t.Id);
            var repoByName = Repos.ToDictionary(r => r.Name);
            var userByLogin = Users.ToDictionary(u => u.Login);

            foreach (var repo in Repos)
            {
                repo.Org = this;
            }

            foreach (var team in Teams)
            {
                team.Org = this;

                if (!string.IsNullOrEmpty(team.ParentId) && teamById.TryGetValue(team.ParentId, out var parentTeam))
                {
                    team.Parent = parentTeam;
                    parentTeam.Children.Add(team);
                }

                foreach (var maintainerLogin in team.MaintainerLogins)
                {
                    if (userByLogin.TryGetValue(maintainerLogin, out var maintainer))
                        team.Maintainers.Add(maintainer);
                }

                foreach (var memberLogin in team.MemberLogins)
                {
                    if (userByLogin.TryGetValue(memberLogin, out var member))
                    {
                        team.Members.Add(member);
                        member.Teams.Add(team);
                    }
                }

                foreach (var repoAccess in team.Repos)
                {
                    repoAccess.Team = team;

                    if (repoByName.TryGetValue(repoAccess.RepoName, out var repo))
                    {
                        repoAccess.Repo = repo;
                        repo.Teams.Add(repoAccess);
                    }
                }

                team.Repos.RemoveAll(r => r.Repo == null);
            }

            foreach (var collaborator in Collaborators)
            {
                if (repoByName.TryGetValue(collaborator.RepoName, out var repo))
                {
                    collaborator.Repo = repo;
                    repo.Users.Add(collaborator);
                }

                if (userByLogin.TryGetValue(collaborator.UserLogin, out var user))
                {
                    collaborator.User = user;
                    user.Repos.Add(collaborator);
                }
            }

            Collaborators.RemoveAll(c => c.Repo == null || c.User == null);

            foreach (var user in Users)
            {
                user.Org = this;
            }

            foreach (var team in Teams)
            {
                var effectiveMembers = team.DescendentsAndSelf().SelectMany(t => t.Members).Distinct();
                team.EffectiveMembers.AddRange(effectiveMembers);
            }

            var orgOwners = Users.Where(u => u.IsOwner).ToArray();

            foreach (var repo in Repos)
            {
                var effectiveUsers = new Dictionary<CachedUser, CachedUserAccess>();

                foreach (var orgOwner in orgOwners)
                {
                    effectiveUsers.Add(orgOwner, new CachedUserAccess
                    {
                        Repo = repo,
                        RepoName = Name,
                        User = orgOwner,
                        UserLogin = orgOwner.Login,
                        Permission = CachedPermission.Admin
                    });
                }

                foreach (var userAccess in repo.Users)
                {
                    if (!userAccess.User.IsOwner)
                        effectiveUsers.Add(userAccess.User, userAccess);
                }

                foreach (var teamAccess in repo.Teams)
                {
                    foreach (var user in teamAccess.Team.EffectiveMembers)
                    {
                        if (effectiveUsers.TryGetValue(user, out var userAccess))
                        {
                            if (userAccess.Permission >= teamAccess.Permission)
                                continue;
                        }

                        effectiveUsers[user] = new CachedUserAccess
                        {
                            Repo = repo,
                            RepoName = Name,
                            User = user,
                            UserLogin = user.Login,
                            Permission = teamAccess.Permission
                        };
                    }
                }

                repo.EffectiveUsers.AddRange(effectiveUsers.Values);
            }
        }

        public static string GetRepoUrl(string orgName, string repoName)
        {
            return $"https://github.com/{orgName}/{repoName}";
        }

        public static string GetTeamUrl(string orgName, string teamSlug)
        {
            return $"https://github.com/orgs/{orgName}/teams/{teamSlug}";
        }

        public static string GetUserUrl(string login, string orgName)
        {
            return $"https://github.com/orgs/{orgName}/people/{login}";
        }

        public static Task<CachedOrg> LoadAsync(Connection connection,
                                                string orgName,
                                                TextWriter logWriter = null,
                                                OspoClient ospoClient = null)
        {
            var loader = new CacheLoader(connection, logWriter, ospoClient);
            return loader.LoadAsync(orgName);
        }

        public static async Task<CachedOrg> LoadAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var stream = File.OpenRead(path))
                return await LoadAsync(stream);
        }

        public static async Task<CachedOrg> LoadAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var orgData = await JsonSerializer.DeserializeAsync<CachedOrg>(stream, options);
            orgData.Initialize();
            return orgData;
        }

        public async Task SaveAsync(string path)
        {
            var cacheDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(cacheDirectory);

            using (var stream = File.Create(path))
                await SaveAsync(stream);
        }

        public async Task SaveAsync(Stream stream)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            await JsonSerializer.SerializeAsync(stream, this, options);
        }
    }
}
