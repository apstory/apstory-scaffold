namespace Apstory.Scaffold.Domain.Util
{
    public static class FileUtils
    {
        public static string SafeReadAllText(string filePath)
        {
            var fileContent = string.Empty;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(fileStream))
                fileContent = reader.ReadToEnd();

            return fileContent;
        }

        public static void WriteTextAndDirectory(string filePath, string fileContents)
        {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // Write the file
            File.WriteAllText(filePath, fileContents);
        }
    }
}
