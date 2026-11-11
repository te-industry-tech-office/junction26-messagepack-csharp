// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ExternalTypes.Library;

/// <summary>A type with no serialization attributes. Its public read-write members are serialized in contractless mode.</summary>
public class ExternalPerson
{
    public int Id { get; set; }

    public string Label { get; set; }

    /// <summary>Gets a value derived from <see cref="Id"/>. Contractless mode skips it because it cannot be deserialized.</summary>
    public int Doubled => this.Id * 2;
}
