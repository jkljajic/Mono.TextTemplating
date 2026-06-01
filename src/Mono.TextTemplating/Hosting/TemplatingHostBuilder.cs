using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TextTemplating;

namespace Mono.TextTemplating
{
	public class TemplatingHostBuilder
	{
		readonly TemplateGenerator generator;
		readonly IServiceProvider serviceProvider;
		TextTemplatingSession session;

		public TemplatingHostBuilder (IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider ?? throw new ArgumentNullException (nameof (serviceProvider));
			this.generator = new TemplateGenerator ();
		}

		public TemplatingHostBuilder () : this (EmptyServiceProvider.Instance) { }

		public TemplatingHostBuilder WithAssemblyReference (string assemblyPath)
		{
			if (!string.IsNullOrEmpty (assemblyPath))
				generator.Refs.Add (assemblyPath);
			return this;
		}

		public TemplatingHostBuilder WithImport (string namespaceName)
		{
			if (!string.IsNullOrEmpty (namespaceName))
				generator.Imports.Add (namespaceName);
			return this;
		}

		public TemplatingHostBuilder WithIncludePath (string path)
		{
			if (!string.IsNullOrEmpty (path))
				generator.IncludePaths.Add (path);
			return this;
		}

		public TemplatingHostBuilder WithReferencePath (string path)
		{
			if (!string.IsNullOrEmpty (path))
				generator.ReferencePaths.Add (path);
			return this;
		}

		public TemplatingHostBuilder WithDirectiveProcessor (string name, string typeName, string assembly)
		{
			if (!string.IsNullOrEmpty (name) && !string.IsNullOrEmpty (typeName) && !string.IsNullOrEmpty (assembly))
				generator.AddDirectiveProcessor (name, typeName, assembly);
			return this;
		}

		public TemplatingHostBuilder WithParameter (string processorName, string directiveName, string parameterName, object value)
		{
			if (!string.IsNullOrEmpty (parameterName) && value != null)
				generator.AddParameter (processorName, directiveName, parameterName, value.ToString ());
			return this;
		}

		public TemplatingHostBuilder WithSession (IDictionary<string, object> sessionState)
		{
			if (sessionState != null) {
				session = new TextTemplatingSession ();
				foreach (var kv in sessionState)
					session[kv.Key] = kv.Value;
			}
			return this;
		}

		public TemplatingHostBuilder WithSessionValue (string key, object value)
		{
			if (session == null)
				session = new TextTemplatingSession ();
			session[key] = value;
			return this;
		}

		public DiTemplateHost Build ()
		{
			return new DiTemplateHost (generator, serviceProvider, session);
		}

		public bool ProcessTemplateFile (string inputFile, string outputFile)
		{
			var host = Build ();
			return host.ProcessTemplateFile (inputFile, outputFile);
		}

		public string ProcessTemplateContent (string inputFileName, string content, ref string outputFileName)
		{
			var host = Build ();
			string output;
			host.ProcessTemplateContent (inputFileName, content, ref outputFileName, out output);
			return output;
		}

		sealed class EmptyServiceProvider : IServiceProvider
		{
			public static readonly EmptyServiceProvider Instance = new ();
			public object GetService (Type serviceType) => null;
		}
	}
}
