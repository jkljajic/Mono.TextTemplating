// 
// CodeDomTemplateCompiler.cs
//  
// Copyright (c) 2025 Mono.TextTemplating Contributors
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

using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;

namespace Mono.TextTemplating.Compilation
{
	/// <summary>
	/// Compiles templates using System.CodeDom (legacy backend, compatible with .NET Framework behavior).
	/// </summary>
	public class CodeDomTemplateCompiler : ITemplateCompiler
	{
		public CompilerResults Compile (CodeCompileUnit compileUnit, TemplateSettings settings, IEnumerable<string> references)
		{
			var pars = new CompilerParameters {
				GenerateExecutable = false,
				CompilerOptions = settings.CompilerOptions,
				IncludeDebugInformation = settings.Debug,
				GenerateInMemory = false,
			};

			foreach (var r in references)
				pars.ReferencedAssemblies.Add (r);

			if (settings.Debug)
				pars.TempFiles.KeepFiles = true;

			return settings.Provider.CompileAssemblyFromDom (pars, compileUnit);
		}
	}
}
