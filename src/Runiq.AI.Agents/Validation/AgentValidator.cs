namespace Runiq.AI.Agents.Validation
{
    /// <summary>
    /// Agent kayitlarinin runtime baslamadan önce geçerli ve tutarli olup olmadigini dogrular.
    /// </summary>
    public static class AgentValidator
    {
        /// <summary>
        /// Kayitli agent listesini dogrular. Hata bulunursa uygulamanin startup sirasinda durmasi için exception firlatir.
        /// </summary>
        public static void ValidateRegisteredAgents(IEnumerable<Agent> agents)
        {
            ArgumentNullException.ThrowIfNull(agents);

            var agentList = agents.ToList();

            ValidateDuplicateIds(agentList);

            foreach (var agent in agentList)
            {
                ValidateAgent(agent);
            }
        }

        private static void ValidateDuplicateIds(IReadOnlyCollection<Agent> agents)
        {
            var duplicateIds = agents
                .GroupBy(agent => agent.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicateIds.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Runiq agent registration failed. Duplicate agent id detected: {string.Join(", ", duplicateIds)}.");
            }
        }

        private static void ValidateAgent(Agent agent)
        {
            ValidateProviderUrl(agent);
            ValidateTimeout(agent);
        }

        private static void ValidateProviderUrl(Agent agent)
        {
            var url = agent.Provider?.Url;

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    $"Runiq agent registration failed. Agent '{agent.Id}' has invalid provider url: '{url}'.");
            }
        }

        private static void ValidateTimeout(Agent agent)
        {
            var timeout = agent.Provider?.Timeout;

            if (timeout is null)
            {
                return;
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Runiq agent registration failed. Agent '{agent.Id}' has invalid provider timeout. Timeout must be greater than zero.");
            }
        }
    }
}
