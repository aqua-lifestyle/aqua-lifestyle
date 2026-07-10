using System;
using System.Linq;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// Value object for an area-space address. Immutable; equality by normalized components.
    /// </summary>
    public sealed class Address : IEquatable<Address>
    {
        public string Street { get; }
        public string City { get; }
        public string Region { get; }
        public string PostalCode { get; }

        public Address(string street, string city, string region, string postalCode)
        {
            Street = (street ?? string.Empty).Trim();
            City = (city ?? string.Empty).Trim();
            Region = (region ?? string.Empty).Trim();
            PostalCode = (postalCode ?? string.Empty).Trim();
        }

        public override string ToString()
            => string.Join(", ", new[] { Street, City, Region, PostalCode }.Where(s => !string.IsNullOrEmpty(s)));

        public bool Equals(Address other)
            => other != null
               && Street == other.Street && City == other.City
               && Region == other.Region && PostalCode == other.PostalCode;

        public override bool Equals(object obj) => Equals(obj as Address);
        public override int GetHashCode() => HashCode.Combine(Street, City, Region, PostalCode);
    }
}
