﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Immutable;

namespace Microsoft.Azure.Functions.Worker.Sdk.Generators
{
    /// <summary>
    /// Generates a class with a method which has code to call the "Configure" method
    /// of each of the participating extension's "WorkerExtensionStartup" implementations.
    /// Also adds the assembly attribute "WorkerExtensionStartupCodeExecutorInfo"
    /// and pass the information(the type) about the class we generated.
    /// We are also inheriting the generated class from the WorkerExtensionStartup class.
    /// (This is the same abstract class extension authors will implement for their extension specific startup code)
    /// We need the same signature as the extension's implementation as our class is an uber class which internally
    /// calls each of the extension's implementations.
    /// </summary>

    // Sample code generated (with one extensions participating in startup hook)
    // There will be one try-catch block for each extension participating in startup hook.

    //[assembly: WorkerExtensionStartupCodeExecutorInfo(typeof(Microsoft.Azure.Functions.Worker.WorkerExtensionStartupCodeExecutor))]
    //
    //internal class WorkerExtensionStartupCodeExecutor : WorkerExtensionStartup
    //{
    //    public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
    //    {
    //        try
    //        {
    //            new Microsoft.Azure.Functions.Worker.Extensions.Http.MyHttpExtensionStartup().Configure(applicationBuilder);
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.Error.WriteLine("Error calling Configure on Microsoft.Azure.Functions.Worker.Extensions.Http.MyHttpExtensionStartup instance." + ex.ToString());
    //        }
    //    }
    //}

    [Generator]
    public class ExtensionStartupRunnerGenerator : ISourceGenerator
    {
        /// <summary>
        /// The attribute which extension authors will apply on an assembly which contains their startup type.
        /// </summary>
        private const string AttributeTypeName = "WorkerExtensionStartupAttribute";

        /// <summary>
        /// Fully qualified name of the above "WorkerExtensionStartupAttribute" attribute.
        /// </summary>
        private const string AttributeTypeFullName = "Microsoft.Azure.Functions.Worker.Core.WorkerExtensionStartupAttribute";

        /// <summary>
        /// Fully qualified name of the base type which extension startup classes should implement.
        /// </summary>
        private const string StartupBaseClassName = "Microsoft.Azure.Functions.Worker.Core.WorkerExtensionStartup";

        public void Execute(GeneratorExecutionContext context)
        {
            var extensionStartupTypeNames = GetExtensionStartupTypes(context);

            if (!extensionStartupTypeNames.Any())
            {
                return;
            }

            var source = GenerateExtensionStartupRunner(context, extensionStartupTypeNames);
            var sourceText = SourceText.From(source, encoding: Encoding.UTF8);

            // Add the source code to the compilation
            context.AddSource($"WorkerExtensionStartupCodeExecutor.g.cs", sourceText);
        }

        /// <summary>
        /// Generates the extension startup source and applies te assembly attribute for the executor.
        /// </summary>
        /// <param name="extensionStartupTypeNames">The types to add to the configuration/bootstrapping process.</param>
        /// <returns>The generated source code.</returns>
        internal string GenerateExtensionStartupRunner(GeneratorExecutionContext context, IList<string> extensionStartupTypeNames)
        {
            string startupCodeExecutor = GenerateStartupCodeExecutorClass(extensionStartupTypeNames);
            var namespaceValue = FunctionsUtil.GetNamespaceForGeneratedCode(context);

            return $$"""
                   // <auto-generated/>
                   using System;
                   using Microsoft.Azure.Functions.Worker;
                   using Microsoft.Azure.Functions.Worker.Core;
       
                   [assembly: WorkerExtensionStartupCodeExecutorInfo(typeof({{namespaceValue}}.WorkerExtensionStartupCodeExecutor))]
       
                   namespace {{namespaceValue}}
                   {
                       internal class WorkerExtensionStartupCodeExecutor : WorkerExtensionStartup
                       {
                           public override void Configure(IFunctionsWorkerApplicationBuilder applicationBuilder)
                           {
                   {{startupCodeExecutor}}
                           }
                       }
                   }
                   """;
        }

        /// <summary>
        /// Gets the extension startup implementation type info from each of the participating extensions.
        /// Each entry in the return type collection includes full type name 
        /// & a potential error message if the startup type is not valid.
        /// </summary>
        private IList<string> GetExtensionStartupTypes(GeneratorExecutionContext context)
        {
            IList<string>? typeNameList = null;

            // Extension authors should decorate their assembly with "WorkerExtensionStartup" attribute
            // if they want to participate in startup.
            foreach (var assembly in context.Compilation.SourceModule.ReferencedAssemblySymbols)
            {
                var extensionStartupAttribute = assembly.GetAttributes()
                                                        .FirstOrDefault(a =>
                                                            (a.AttributeClass?.Name.Equals(AttributeTypeName,
                                                                StringComparison.Ordinal) ?? false) &&
                                                                        //Call GetFullName only if class name matches.
                                                                        a.AttributeClass.GetFullName()
                                                                .Equals(AttributeTypeFullName,
                                                                    StringComparison.Ordinal));
                if (extensionStartupAttribute != null)
                {
                    // WorkerExtensionStartupAttribute has a constructor with one param, the type of startup implementation class.
                    var firstConstructorParam = extensionStartupAttribute.ConstructorArguments[0];
                    if (firstConstructorParam.Value is not ITypeSymbol typeSymbol)
                    {
                        continue;
                    }

                    var fullTypeName = typeSymbol.ToDisplayString();
                    var hasAnyError = ReportDiagnosticErrorsIfAny(context, typeSymbol);

                    if (!hasAnyError)
                    {
                        typeNameList ??= new List<string>();
                        typeNameList.Add(fullTypeName);
                    }
                }
            }

            return typeNameList ?? ImmutableList<string>.Empty;
        }

        /// <summary>
        /// Check the startup type implementation is valid and report Diagnostic errors if it is not valid.
        /// </summary>
        private static bool ReportDiagnosticErrorsIfAny(GeneratorExecutionContext context, ITypeSymbol typeSymbol)
        {
            var hasAnyError = false;
            
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                // Check public parameterless constructor exist for the type.
                var constructorExist = namedTypeSymbol.InstanceConstructors
                                                  .Any(c => c.Parameters.Length == 0 &&
                                                            c.DeclaredAccessibility == Accessibility.Public);
                if (!constructorExist)
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ConstructorMissing, Location.None,
                        typeSymbol.ToDisplayString()));
                    hasAnyError = true;
                }

                // Check the extension startup class implements WorkerExtensionStartup abstract class.
                if (!namedTypeSymbol.BaseType!.GetFullName().Equals(StartupBaseClassName, StringComparison.Ordinal))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.IncorrectBaseType, Location.None,
                        typeSymbol.ToDisplayString(), StartupBaseClassName));
                    hasAnyError = true;
                }
            }

            return hasAnyError;
        }

        /// <summary>
        /// Writes a class with code which calls the Configure method on each implementation of participating extensions.
        /// We also have it implement the same "IWorkerExtensionStartup" interface which extension authors implement.
        /// </summary>
        private static string GenerateStartupCodeExecutorClass(IList<string> startupTypeNames)
        {
           
            var builder = new StringBuilder();
            for (int i = 0; i < startupTypeNames.Count; i++)
            {
                var typeName = startupTypeNames[i];

                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append($$"""
                                    try
                                    {
                                        new {{typeName}}().Configure(applicationBuilder);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.Error.WriteLine("Error calling Configure on {{typeName}} instance."+ex.ToString());
                                    }
                        """);
            }

            return builder.ToString();
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}
