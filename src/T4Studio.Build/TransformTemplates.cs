// 
// TransformTemplates.cs
//  
// Copyright (c) 2025 T4Studio Contributors
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace T4Studio.Build
{
	/// <summary>
	/// MSBuild task that transforms .tt (T4) template files into generated output files.
	/// Supports both runtime template processing and preprocessed template generation.
	/// </summary>
	public class TransformTemplates : Task
	{
		/// <summary>
		/// The .tt template files to transform.
		/// </summary>
		[Required]
		public ITaskItem[] Templates { get; set; } = Array.Empty<ITaskItem> ();

		/// <summary>
		/// Directory for generated output files. Defaults to $(IntermediateOutputPath)GeneratedT4.
		/// </summary>
		public string OutputDir { get; set; } = string.Empty;

		/// <summary>
		/// Assembly references to pass to the T4 engine.
		/// </summary>
		public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem> ();

		/// <summary>
		/// Namespaces to import into the generated code.
		/// </summary>
		public string[] Imports { get; set; } = Array.Empty<string> ();

		/// <summary>
		/// Additional include paths for <#@ include #> directives.
		/// </summary>
		public string[] IncludePaths { get; set; } = Array.Empty<string> ();

		/// <summary>
		/// Additional reference search paths.
		/// </summary>
		public string[] ReferencePaths { get; set; } = Array.Empty<string> ();

		/// <summary>
		/// If true, generates preprocessed templates (runtime classes) instead of output files.
		/// </summary>
		public bool Preprocess { get; set; }

		/// <summary>
		/// The generated output files, to be added to the Compile item group.
		/// </summary>
		[Output]
		public ITaskItem[] GeneratedFiles { get; set; } = Array.Empty<ITaskItem> ();

		public override bool Execute ()
		{
			if (Templates.Length == 0) {
				Log.LogMessage (MessageImportance.Low, "No .tt templates found to transform.");
				return true;
			}

			var generatedFiles = new List<ITaskItem> ();
			bool success = true;

			foreach (var templateItem in Templates) {
				var templatePath = templateItem.GetMetadata ("FullPath");
				if (string.IsNullOrEmpty (templatePath) || !File.Exists (templatePath)) {
					Log.LogWarning ("Template file not found: {0}", templateItem.ItemSpec);
					continue;
				}

				try {
					var result = TransformTemplate (templateItem, templatePath);
					if (result != null) {
						generatedFiles.Add (result);
						Log.LogMessage (MessageImportance.Normal, "Transformed: {0} → {1}", templatePath, result.ItemSpec);
					}
				} catch (Exception ex) {
					Log.LogError ("Error transforming {0}: {1}", templatePath, ex.Message);
					success = false;
				}
			}

			GeneratedFiles = generatedFiles.ToArray ();
			return success && !Log.HasLoggedErrors;
		}

		ITaskItem TransformTemplate (ITaskItem templateItem, string templatePath)
		{
			string outputFile;
			if (!string.IsNullOrEmpty (OutputDir)) {
				var relativeDir = templateItem.GetMetadata ("RecursiveDir");
				var lastGenOutput = templateItem.GetMetadata ("LastGenOutput");
				if (!string.IsNullOrEmpty (lastGenOutput)) {
					var genFileName = Path.GetFileName (lastGenOutput);
					outputFile = Path.Combine (OutputDir, relativeDir, genFileName);
				} else {
					var fileName = Path.GetFileNameWithoutExtension (templatePath);
					outputFile = Path.Combine (OutputDir, relativeDir, fileName + (Preprocess ? ".cs" : ".generated.cs"));
				}
			} else {
				outputFile = templateItem.GetMetadata ("LastGenOutput");
				if (string.IsNullOrEmpty (outputFile)) {
					outputFile = Path.ChangeExtension (templatePath, ".generated.cs");
				} else if (!Path.IsPathRooted (outputFile)) {
					var dir = Path.GetDirectoryName (templatePath);
					outputFile = Path.Combine (dir ?? ".", outputFile);
				}
			}

			var generator = new TemplateGenerator ();

			// Add standard references
			foreach (var reference in References) {
				var refPath = reference.GetMetadata ("FullPath");
				if (!string.IsNullOrEmpty (refPath))
					generator.Refs.Add (refPath);
				else if (!string.IsNullOrEmpty (reference.ItemSpec))
					generator.Refs.Add (reference.ItemSpec);
			}

			// Add imports
			foreach (var import in Imports) {
				if (!string.IsNullOrEmpty (import))
					generator.Imports.Add (import);
			}

			// Add include paths
			foreach (var path in IncludePaths) {
				if (!string.IsNullOrEmpty (path))
					generator.IncludePaths.Add (path);
			}

			// Add reference paths
			foreach (var path in ReferencePaths) {
				if (!string.IsNullOrEmpty (path))
					generator.ReferencePaths.Add (path);
			}

			// Pass custom parameters from item metadata (T4Param_Key=Value)
			foreach (string metadataName in templateItem.MetadataNames) {
				if (metadataName.StartsWith ("T4Param_", StringComparison.OrdinalIgnoreCase)) {
					var paramName = metadataName.Substring ("T4Param_".Length);
					var paramValue = templateItem.GetMetadata (metadataName);
					if (!string.IsNullOrEmpty (paramName) && paramValue != null)
						generator.AddParameter (null, null, paramName, paramValue);
				}
			}

			var outputDir = Path.GetDirectoryName (outputFile);
			if (!string.IsNullOrEmpty (outputDir) && !Directory.Exists (outputDir))
				Directory.CreateDirectory (outputDir);

			if (Preprocess) {
				string className = Path.GetFileNameWithoutExtension (templatePath)
					.Replace (".", "_").Replace ("-", "_").Replace (" ", "_");
				string classNamespace = "GeneratedT4";
				string language;
				string[] references;

				generator.PreprocessTemplate (templatePath, className, classNamespace,
					outputFile, System.Text.Encoding.UTF8, out language, out references);
			} else {
				generator.ProcessTemplate (templatePath, outputFile);
			}

			// Log errors from the generator
			foreach (System.CodeDom.Compiler.CompilerError error in generator.Errors) {
				if (error.IsWarning) {
					Log.LogWarning (null, null, null, error.FileName, error.Line, error.Column,
						0, 0, "{0}", error.ErrorText);
				} else {
					Log.LogError (null, null, null, error.FileName, error.Line, error.Column,
						0, 0, "{0}", error.ErrorText);
				}
			}

			if (generator.Errors.HasErrors)
				return null;

			// Create the output item
			var outputItem = new TaskItem (outputFile);
			outputItem.SetMetadata ("AutoGen", "true");
			outputItem.SetMetadata ("T4Template", templatePath);
			return outputItem;
		}
	}
}

