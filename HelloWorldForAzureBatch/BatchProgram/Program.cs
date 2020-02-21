using System.IO;

namespace HelloWorldForAzureBatch
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFilePath = args[0];
            string outputFileDirectory = args[1];

            if(!outputFileDirectory.EndsWith('\\'))
            {
                outputFileDirectory += "\\";
            }

            string inputFileName = ReadFile(inputFilePath);
            WriteOutput(inputFileName, outputFileDirectory);
        }

        private static string ReadFile(string filePath)
        {
            if(!File.Exists(filePath))
            {
                filePath = Directory.GetCurrentDirectory() + filePath;
            }
            File.ReadAllText(filePath);
            return Path.GetFileName(filePath);
        }

        private static void WriteOutput(string fileName, string outputFileDirectory)
        {
            if(!outputFileDirectory.Contains(Directory.GetCurrentDirectory()))
            {
                outputFileDirectory = Directory.GetCurrentDirectory() + outputFileDirectory;
                if(!Directory.Exists(outputFileDirectory))
                {
                    Directory.CreateDirectory(outputFileDirectory);
                }
            }
            File.WriteAllText($"{outputFileDirectory}{fileName}", $"Batch program successfully read {fileName}. Hurray!");
        }
    }
}
