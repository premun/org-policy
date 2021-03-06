﻿using Microsoft.DotnetOrg.GitHubCaching;

namespace Microsoft.DotnetOrg.PolicyCop.Reporting
{
    internal struct ReportRow
    {
        public ReportRow(CachedRepo repo = null, CachedTeam team = null, CachedUser user = null, CachedUserAccess userAccess = null, CachedTeamAccess teamAccess = null, CachedWhatIfPermission? whatIfPermission = null)
        {
            Repo = repo;
            Team = team;
            User = user;
            UserAccess = userAccess;
            TeamAccess = teamAccess;
            WhatIfPermission = whatIfPermission;
        }

        public CachedOrg Org
        {
            get
            {
                if (Repo != null) return Repo.Org;
                if (Team != null) return Team.Org;
                if (User != null) return User.Org;
                if (UserAccess != null) return UserAccess.Org;
                if (TeamAccess != null) return TeamAccess.Org;
                return null;
            }
        }

        public CachedRepo Repo { get; }
        public CachedTeam Team { get; }
        public CachedUser User { get; }
        public CachedUserAccess UserAccess { get; }
        public CachedTeamAccess TeamAccess { get; }
        public CachedWhatIfPermission? WhatIfPermission { get; }
    }
}
