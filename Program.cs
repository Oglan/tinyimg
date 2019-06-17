using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;

namespace tinyimg
{
    class Program
    {
        private const int _maxQuality = 100;
        private const int _minQuality = 1;
        private const double _defaultEps = 0.02d;

        static void Main(string[] args)
        {
            var files = ParseArgs(args, out double eps);

            if (!files.Any())
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("\ttinyimg [/k] source-image [destination-image]");
                Console.WriteLine("\ttinyimg [/k] source-image [source-image2 source-image3 ...]");
                return;
            }

            foreach (var (inName, outName) in files)
            {
#if DEBUG
                Console.WriteLine($"Optimizing image {inName}. Destination file name: {outName}. Eps: {eps}");
#endif
                ProcessImage(inName, outName, eps);
            }
        }

        private static void ProcessImage(string inName, string outName, double eps)
        {
            var tmp = new MemoryStream();
            using (var src = File.OpenRead(inName))
                src.CopyTo(tmp);
            tmp.Position = 0;

            var quality = GetOptimalCompressionQuality(tmp, eps);
            var img = new MagickImage(tmp)
            {
                FilterType = FilterType.LanczosSharp,
                ColorType = ColorType.Optimize,
                Quality = quality
            };
            using (var dst = File.Create(outName))
                img.Write(dst);
            var imageOptimizer = new ImageOptimizer() { OptimalCompression = true };
            imageOptimizer.Compress(outName);
        }

        private static int GetOptimalCompressionQuality(Stream input, double eps)
        {
            var max = _maxQuality;
            var min = _minQuality;
            var middle = 0;

            var original = new MagickImage(input);

            while (max - min > 1)
            {
                var tmpImage = new MagickImage(input);
                middle = (max + min) / 2;
                tmpImage.Quality = middle;
                var buff = new MemoryStream();
                tmpImage.Write(buff);
                buff.Position = 0;
                var diff = original.Compare(new MagickImage(buff), ErrorMetric.Fuzz);
#if DEBUG
                Console.WriteLine($"Diff for compression {middle}: {diff}");
#endif
                if (diff > eps)
                    min = middle;
                else
                    max = middle;
            }

            return middle;
        }

        private static IList<(string inName, string outName)> ParseArgs(string[] args, out double eps)
        {
            var result = new List<(string inName, string outName)>();
            eps = _defaultEps;

            bool overwrite = true;
            var files = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg == "/?" || arg == "--help" || arg == "-h")
                {
                    result.Clear();
                    return result;
                }
                else if (arg == "/e")
                {
                    if (i + 1 < args.Length && double.TryParse(args[i + 1], out eps))
                    {
                        i++;
                    }
                    else
                    {
                        result.Clear();
                        return result;
                    }
                }
                else if (arg == "/k")
                    overwrite = false;
                else
                    files.Add(arg);
            }

            if (!files.Any())
            {
                result.Clear();
                return result;
            }

            if (files.Count == 2 && !overwrite)
            {
                result.Add((files[0], files[1]));
            }
            else
            {
                foreach (var file in files)
                {
                    var inName = file;
                    var outName = overwrite
                        ? file
                        : Path.Combine(Path.GetDirectoryName(inName), Path.GetFileNameWithoutExtension(inName) + "_tiny" + Path.GetExtension(inName));
                    result.Add((inName, outName));
                }
            }

            return result;
        }
    }
}
