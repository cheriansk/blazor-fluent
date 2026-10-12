using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorFluent.Core.Domain.ValueObjects;

/// <summary>
/// EF Core 10 ComplexType Value Object for physical addresses.
/// Embedded inline within host entities without requiring dedicated foreign key or shadow ID columns.
/// </summary>
[ComplexType]
public record Address
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;

    public Address() { }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }
}
