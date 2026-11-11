// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MessagePack.SourceGenerator.CodeAnalysis;

/// <summary>
/// A partial formatter class marked with <c>[GeneratedMessagePackFormatter]</c>, and the optional surrogate type it names.
/// </summary>
/// <param name="Formatter">The user-declared partial formatter class.</param>
/// <param name="Surrogate">The surrogate type named by <c>SurrogateType</c>, or <see langword="null"/> for contractless mode.</param>
public sealed record GeneratedFormatterRequest(INamedTypeSymbol Formatter, INamedTypeSymbol? Surrogate);
