﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotnetOrg.Policies
{
    public static class PolicyRunner
    {
        public static IReadOnlyList<PolicyRule> GetRules()
        {
            return typeof(PolicyRunner).Assembly
                                        .GetTypes()
                                        .Where(t => !t.IsAbstract &&
                                                    t.GetConstructor(Array.Empty<Type>()) != null &&
                                                    typeof(PolicyRule).IsAssignableFrom(t))
                                        .Select(t => Activator.CreateInstance(t))
                                        .Cast<PolicyRule>()
                                        .ToList();
        }

        public static Task RunAsync(PolicyAnalysisContext context)
        {
            var rules = GetRules();
            return RunAsync(context, rules);
        }

        public static Task RunAsync(PolicyAnalysisContext context, IEnumerable<PolicyRule> rules)
        {
            var ruleTasks = rules.Select(r => r.GetViolationsAsync(context));
            return Task.WhenAll(ruleTasks);
        }
    }
}
