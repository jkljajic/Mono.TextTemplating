using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace T4Studio.Vsix
{
    [Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [ComVisible(true)]
    public sealed class T4SingleFileGenerator : IVsSingleFileGenerator
    {
        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".generated.txt";
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            pcbOutput = 0;

            try
            {
                var generator = new TemplateGenerator();

                AddDefaultReferences(generator);
                generator.IncludePaths.Add(Path.GetDirectoryName(wszInputFilePath) ?? ".");

                var outputFileName = string.Empty;
                string outputContent;

                var success = generator.ProcessTemplate(wszInputFilePath, bstrInputFileContents, ref outputFileName, out outputContent);

                if (!success || generator.Errors.HasErrors)
                {
                    for (int i = 0; i < generator.Errors.Count; i++)
                    {
                        var error = generator.Errors[i];
                        pGenerateProgress?.GeneratorError(error.IsWarning ? 1 : 0, 0, error.ErrorText, (uint)error.Line, (uint)error.Column);
                    }
                    return VSConstants.E_FAIL;
                }

                if (string.IsNullOrEmpty(outputContent))
                {
                    pGenerateProgress?.GeneratorError(0, 0, "Template generated empty output", 0, 0);
                    return VSConstants.E_FAIL;
                }

                pGenerateProgress?.Progress(100, 100);

                var bytes = System.Text.Encoding.UTF8.GetBytes(outputContent);
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
                pcbOutput = (uint)bytes.Length;

                return VSConstants.S_OK;
            }
            catch (Exception ex)
            {
                pGenerateProgress?.GeneratorError(0, 0, $"T4 generation error: {ex.Message}", (uint)ex.HResult, 0);
                return VSConstants.E_FAIL;
            }
        }

        static void AddDefaultReferences(TemplateGenerator generator)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrEmpty(asm.Location) && !generator.Refs.Contains(asm.Location))
                        generator.Refs.Add(asm.Location);
                }
                catch { }
            }
        }
    }
}

