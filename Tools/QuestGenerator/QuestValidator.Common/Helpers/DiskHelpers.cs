using System.IO;

namespace QuestValidator.Helpers
{
    public static class DiskHelpers
    {
        public static void CreateDirIfDoesntExist(string path)
        {
            if (!Directory.Exists($"{path}"))
            {
                //create dump dir
                Directory.CreateDirectory($"{path}");
            }
        }

        public static string[] GetJsonFiles(string path)
        {
            return Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly);
        }

        public static string CreateWorkingFolders()
        {
            var workingPath = Directory.GetCurrentDirectory();
            // create input folder
            var inputPath = $"{workingPath}//input";
            DiskHelpers.CreateDirIfDoesntExist(inputPath);

            return inputPath;
        }
    }
}
