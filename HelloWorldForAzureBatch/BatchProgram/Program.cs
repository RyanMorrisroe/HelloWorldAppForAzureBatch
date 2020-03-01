using System;
using System.IO;

namespace HelloWorldForAzureBatch
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFilePath = args[0];
            string outputFileDirectory = args[1];

            Console.WriteLine("Program has been called");
            if (!outputFileDirectory.EndsWith('\\'))
            {
                outputFileDirectory += "\\";
            }

            string inputFileName = ReadFile(inputFilePath);
            Console.WriteLine($"Input file read, filename is {inputFileName}");
            WriteOutput(inputFileName, outputFileDirectory);
            Directory.CreateDirectory(outputFileDirectory);
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
            Console.WriteLine($"Checking for output file directory {outputFileDirectory}");
            if(!Directory.Exists(outputFileDirectory))
            {
                Console.WriteLine("Output file directory not found, creating");
                Directory.CreateDirectory(outputFileDirectory);
            }
            Console.WriteLine("Writing text to output file");
            File.WriteAllText($"{outputFileDirectory}{fileName}", $"Batch program successfully read {fileName}. Hurray!");
        }
    }
}