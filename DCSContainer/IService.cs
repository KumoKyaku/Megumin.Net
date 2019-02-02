namespace Megumin.DCS
{
    public interface IService
    {
        int GUID { get; set; }
        void Update(double deltaTime);
        void Start();
    }
}