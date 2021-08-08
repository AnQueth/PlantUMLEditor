namespace PlantUMLEditor.Models
{
    internal class AppConfiguration : IConfiguration
    {
        public AppConfiguration(string jarLocation)
        {
            JarLocation = jarLocation;
        }

        public string JarLocation { get; set; }
    }
}