using System.Configuration;
using System.Web;

namespace WebDecouverteAzure.Services
{
    /// <summary>
    /// Repository des paramétrages. Les données sont stockées dans le cache du site !
    /// </summary>
    /// <remarks>Dans la vraie vie, on doit accéder plutôt à une base de données.
    /// Ici, ce repository est trop typé "web", ne supporte pas les farms, etc. Bref, il n'est utile que pour une démo !</remarks>
    public class ParametrageRepository : IParametrageRepository
    {
        private const string ParametrageKey = "Duration";
        public void Save(int duration)
        {
            HttpContext.Current.Cache[ParametrageKey] = duration;
        }

        public int Load()
        {
            if (HttpContext.Current.Cache[ParametrageKey] == null)
                HttpContext.Current.Cache[ParametrageKey] = int.Parse(ConfigurationManager.AppSettings[ParametrageKey]);

            return (int)HttpContext.Current.Cache[ParametrageKey];
        }
    }
}