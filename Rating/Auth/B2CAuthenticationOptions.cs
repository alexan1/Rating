namespace Rating.Auth
{
    public sealed class B2CAuthenticationOptions
    {
        public string Authority { get; init; }

        public string Audience { get; init; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Authority) &&
            !string.IsNullOrWhiteSpace(Audience);
    }
}
