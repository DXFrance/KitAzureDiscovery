using System;

namespace WebDecouverteAzure.Services
{
    /// <summary>
    /// Service utilisant un <see cref="IParametrageRepository"/> pour la sauvegarde.
    /// </summary>
    /// <remark>Si vous voulez aller plus loin, implémentez un repository accédant plutôt à une base de données.</remark>
    public class ParametrageService
    {
        private readonly IParametrageRepository _parametrageRepository;

        public ParametrageService() : this(new ParametrageRepository()){}
        public ParametrageService(IParametrageRepository parametrageRepository)
        {
            _parametrageRepository = parametrageRepository;
        }
        public int LoadDuration()
        {
            return _parametrageRepository.Load();
        }

        public void SaveDuration(int duration)
        {
            if (duration < 5)
                throw new ArgumentException("La durée ne peut être inférieure à 5 secondes");

            _parametrageRepository.Save(duration);
        }
    }
}
