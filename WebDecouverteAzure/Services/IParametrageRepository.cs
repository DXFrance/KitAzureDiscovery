namespace WebDecouverteAzure.Services
{
    public interface IParametrageRepository
    {
        void Save(int duration);
        int Load();
    }
}