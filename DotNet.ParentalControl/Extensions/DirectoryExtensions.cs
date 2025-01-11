using System.Text.RegularExpressions;

namespace DotNet.ParentalControl.Extensions
{
    public static class DirectoryExtensions
    {
        private static readonly Regex SpecialPath = new Regex("\\%(?<name>[^\\%]+)\\%\\\\", RegexOptions.Compiled);

        public static string ResolveSpecialFolders(string path)
        {
            var specialPath = SpecialPath.Match(path);
            if (specialPath.Success)
            {
                var specialFolder = specialPath.Groups["name"].Value switch
                {
                    "DESKTOP" => Environment.SpecialFolder.Desktop,
                    "PROGRAMFILES" => Environment.SpecialFolder.ProgramFiles,
                    "PROGRAMFILESX86" => Environment.SpecialFolder.ProgramFilesX86,
                    "COMMONPROGRAMFILES" => Environment.SpecialFolder.CommonProgramFiles,
                    "COMMONPROGRAMFILESX86" => Environment.SpecialFolder.CommonProgramFilesX86,
                    "LOCALAPPDATA" => Environment.SpecialFolder.LocalApplicationData,
                    "APPDATA" => Environment.SpecialFolder.ApplicationData,
                    "MYDOCUMENTS" => Environment.SpecialFolder.MyDocuments,
                    "FAVORITES" => Environment.SpecialFolder.Favorites,
                    _ => throw new NotSupportedException($"Not supported: {specialPath}")
                };

                return Environment.GetFolderPath(specialFolder) + "\\" + path.Substring(specialPath.Length);
            }
            else
                return path;
        }
    }
}
