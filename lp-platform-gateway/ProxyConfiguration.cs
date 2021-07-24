using System.Collections.Generic;

namespace lp_platform_gateway
{
    public class ProxyConfiguration
    {
        public string DefaultURL { get; set; }
        public string Stage { get; set; }
        public List<RedirectQueries> RedirectQueries { get; set; } = new List<RedirectQueries>();
    }

    public class RedirectQueries
    {
        public string Query { get; set; }
        public string RedirectURL { get; set; }
    }
}
