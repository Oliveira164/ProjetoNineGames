namespace ProjetoNineGames.Autenticacao
{
    public static class SessionKeys
    {
        public const string UserId        = "UserId";
        public const string UserName      = "UserName";
        public const string UserEmail     = "UserEmail";
        public const string UserRole      = "UserRole";

        // Chave usada durante o fluxo de 2FA:
        // guarda o ID do usuário que passou na 1ª etapa (email/senha)
        // mas ainda não confirmou o código TOTP.
        public const string TwoFaPendingUserId = "TwoFaPendingUserId";
    }
}
