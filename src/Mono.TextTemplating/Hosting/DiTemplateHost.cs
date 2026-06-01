using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TextTemplating;

namespace Mono.TextTemplating
{
	public class DiTemplateHost : MarshalByRefObject, ITextTemplatingEngineHost, ITextTemplatingSessionHost
	{
		readonly TemplateGenerator generator;
		readonly IServiceProvider serviceProvider;
		readonly Dictionary<string, object> hostOptions = new Dictionary<string, object> (StringComparer.OrdinalIgnoreCase);

		public DiTemplateHost (TemplateGenerator generator, IServiceProvider serviceProvider, ITextTemplatingSession session)
		{
			this.generator = generator ?? throw new ArgumentNullException (nameof (generator));
			this.serviceProvider = serviceProvider ?? throw new ArgumentNullException (nameof (serviceProvider));
			this.Session = session ?? new TextTemplatingSession ();
		}

		public void SetHostOption (string optionName, object value)
		{
			hostOptions[optionName] = value;
		}

		public IServiceProvider ServiceProvider => serviceProvider;

		public T GetService<T> () where T : class
		{
			return serviceProvider.GetService (typeof (T)) as T;
		}

		public TemplateGenerator Generator => generator;
		public CompilerErrorCollection Errors => generator.Errors;
		public ITextTemplatingSession Session { get; set; }

		public object GetHostOption (string optionName)
		{
			if (hostOptions.TryGetValue (optionName, out var value))
				return value;
			if (string.Equals (optionName, "ServiceProvider", StringComparison.OrdinalIgnoreCase))
				return serviceProvider;
			return generator.GetHostOption (optionName);
		}

		public bool LoadIncludeText (string requestFileName, out string content, out string location)
		{
			return ((ITextTemplatingEngineHost) generator).LoadIncludeText (requestFileName, out content, out location);
		}

		public void LogErrors (CompilerErrorCollection errors)
		{
			((ITextTemplatingEngineHost) generator).LogErrors (errors);
		}

		public AppDomain ProvideTemplatingAppDomain (string content)
		{
			return generator.ProvideTemplatingAppDomain (content);
		}

		public string ResolveAssemblyReference (string assemblyReference)
		{
			return ((ITextTemplatingEngineHost) generator).ResolveAssemblyReference (assemblyReference);
		}

		public Type ResolveDirectiveProcessor (string processorName)
		{
			return ((ITextTemplatingEngineHost) generator).ResolveDirectiveProcessor (processorName);
		}

		public string ResolveParameterValue (string directiveId, string processorName, string parameterName)
		{
			var result = ((ITextTemplatingEngineHost) generator).ResolveParameterValue (directiveId, processorName, parameterName);
			if (result == null && parameterName != null) {
				// Fall back to DI: resolve parameter name as service type
				var serviceType = Type.GetType (parameterName, throwOnError: false);
				if (serviceType != null) {
					var service = serviceProvider.GetService (serviceType);
					result = service?.ToString ();
				}
			}
			return result;
		}

		public string ResolvePath (string path)
		{
			return ((ITextTemplatingEngineHost) generator).ResolvePath (path);
		}

		public void SetFileExtension (string extension)
		{
			((ITextTemplatingEngineHost) generator).SetFileExtension (extension);
		}

		public void SetOutputEncoding (Encoding encoding, bool fromOutputDirective)
		{
			((ITextTemplatingEngineHost) generator).SetOutputEncoding (encoding, fromOutputDirective);
		}

		public IList<string> StandardAssemblyReferences => ((ITextTemplatingEngineHost) generator).StandardAssemblyReferences;
		public IList<string> StandardImports => ((ITextTemplatingEngineHost) generator).StandardImports;
		public string TemplateFile => ((ITextTemplatingEngineHost) generator).TemplateFile;

		public ITextTemplatingSession CreateSession () => new TextTemplatingSession ();

		// Convenience: file-based processing
		public bool ProcessTemplateFile (string inputFile, string outputFile)
		{
			return generator.ProcessTemplate (inputFile, outputFile);
		}

		// Convenience: content-based processing
		public bool ProcessTemplateContent (string inputFileName, string inputContent, ref string outputFileName, out string outputContent)
		{
			return generator.ProcessTemplate (inputFileName, inputContent, ref outputFileName, out outputContent);
		}

		// Convenience: preprocess
		public bool PreprocessTemplate (string inputFile, string className, string classNamespace,
			string outputFile, Encoding encoding, out string language, out string[] references)
		{
			return generator.PreprocessTemplate (inputFile, className, classNamespace, outputFile, encoding, out language, out references);
		}
	}
}
