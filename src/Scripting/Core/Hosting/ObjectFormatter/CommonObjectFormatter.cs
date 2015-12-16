// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.CodeAnalysis.Scripting.Hosting
{
    /// <summary>
    /// Object pretty printer.
    /// </summary>
    public abstract partial class CommonObjectFormatter : ObjectFormatter
    {
        public override string FormatObject(object obj, PrintOptions options)
        {
            if (options == null)
            {
                // We could easily recover by using default options, but it makes
                // more sense for the host to choose the defaults so we'll require
                // that options be passed.
                throw new ArgumentNullException(nameof(options));
            }

            var visitor = new Visitor(this, GetInternalBuilderOptions(options), GetPrimitiveOptions(options), GetTypeNameOptions(options), options.MemberDisplayFormat);
            return visitor.FormatObject(obj);
        }

        protected virtual ObjectFilter Filter { get; } = new CommonObjectFilter();

        protected abstract CommonTypeNameFormatter TypeNameFormatter { get; }
        protected abstract CommonPrimitiveFormatter PrimitiveFormatter { get; }

        internal virtual BuilderOptions GetInternalBuilderOptions(PrintOptions printOptions) =>
            new BuilderOptions(
                indentation: "  ",
                newLine: Environment.NewLine,
                ellipsis: printOptions.Ellipsis,
                maximumLineLength: int.MaxValue,
                maximumOutputLength: printOptions.MaximumOutputLength);

        protected virtual CommonPrimitiveFormatterOptions GetPrimitiveOptions(PrintOptions printOptions) =>
            new CommonPrimitiveFormatterOptions(
                useHexadecimalNumbers: printOptions.NumberRadix == NumberRadix.Hexadecimal,
                includeCodePoints: printOptions.EscapeNonPrintableCharacters, // TODO (acasey): not quite the same
                omitStringQuotes: false);

        protected virtual CommonTypeNameFormatterOptions GetTypeNameOptions(PrintOptions printOptions) =>
            new CommonTypeNameFormatterOptions(
                useHexadecimalArrayBounds: printOptions.NumberRadix == NumberRadix.Hexadecimal,
                showNamespaces: false);

        public override string FormatUnhandledException(Exception e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            var pooled = PooledStringBuilder.GetInstance();
            var builder = pooled.Builder;

            builder.AppendLine(e.Message);

            var trace = new StackTrace(e, needFileInfo: true);
            foreach (var frame in Filter.Filter(trace.GetFrames()))
            {
                var method = frame.GetMethod();
                var methodDisplay = FormatMethodSignature(method);

                if (methodDisplay == null)
                {
                    continue;
                }

                builder.Append("  + ");
                builder.Append(methodDisplay);

                var fileName = frame.GetFileName();
                if (fileName != null)
                {
                    builder.Append(string.Format(CultureInfo.CurrentUICulture, ScriptingResources.AtFileLine, fileName, frame.GetFileLineNumber()));
                }

                builder.AppendLine();
            }

            return pooled.ToStringAndFree();
        }

        /// <summary>
        /// Returns a method signature display string. Used to display stack frames.
        /// </summary>
        /// <returns>Null if the method is a compiler generated method that shouldn't be displayed to the user.</returns>
        internal virtual string FormatMethodSignature(MethodBase method)
        {
            var pooled = PooledStringBuilder.GetInstance();
            var builder = pooled.Builder;

            var declaringType = method.DeclaringType;
            var options = new CommonTypeNameFormatterOptions(useHexadecimalArrayBounds: false, showNamespaces: true);

            builder.Append(TypeNameFormatter.FormatTypeName(declaringType, options));
            builder.Append('.');
            builder.Append(method.Name);
            builder.Append(TypeNameFormatter.FormatTypeArguments(method.GetGenericArguments(), options));

            builder.Append('(');

            bool first = true;
            foreach (var parameter in method.GetParameters())
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append(", ");
                }

                builder.Append(FormatRefKind(parameter));
                builder.Append(TypeNameFormatter.FormatTypeName(parameter.ParameterType, options));
            }

            builder.Append(')');

            return pooled.ToStringAndFree();
        }

        protected abstract string FormatRefKind(ParameterInfo parameter);
    }
}
