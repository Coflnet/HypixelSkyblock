namespace hypixel.Prediction
{
    /// <summary>
    /// Mapps db to model format
    /// </summary>
    public class Mapper
    {
        public static Mapper Instance {get;}

        static Mapper()
        {
            Instance = new Mapper();
        }

    }
}