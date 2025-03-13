namespace Snack.Bar.Auth.Register.Models
{
    public struct UserRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string? Phone { get; set; }

        public override readonly string ToString()
        {
            return $"Name: {Name} | Email: {Email} | Password: {Password} | Phone {Phone}";
        }
    }
}
