using FollowLifeAPI.DataLayer;

namespace FollowLifeAPI.BE
{
    public class AddressBE
    {
        public string Street { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string Complement { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;

        public AddressBE Fill(Address a)
        {
            if (a is null)
                return null;

            this.Street = a.Street;
            this.Neighborhood = a.Neighborhood;
            this.Number = a.Number;
            this.Complement = string.IsNullOrEmpty(a.Complement) ? null : a.Complement;
            this.District = a.District.Name;

            return this;
        }
    }
}