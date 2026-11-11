// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ExternalTypes.Library;

/// <summary>A type with no serialization attributes whose wire contract is described by a surrogate type in the consuming assembly.</summary>
public class ExternalWidget
{
    public int Id { get; set; }

    public string Name { get; set; }

    /// <summary>Gets or sets a value that is not part of the surrogate's contract, so it is not serialized.</summary>
    public int Internal { get; set; }
}
