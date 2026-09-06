using System.Collections.Generic;
using System.Linq;
using QuestValidator.Helpers;

namespace AssortGenerator.Common.Helpers
{
    public static class InputFileHelper
    {
        private static List<string> _inputFilePaths;

        public static void SetInputFiles(string path)
        {
            _inputFilePaths = DiskHelpers.GetJsonFiles(path).ToList();
        }

        public static IEnumerable<string> GetInputFilePaths()
        {
            return _inputFilePaths;
        }
    }
}
