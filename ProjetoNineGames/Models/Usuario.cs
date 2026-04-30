namespace ProjetoNineGames.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Email { get; set; }

        //HASH da senha
        public string SenhaHash { get; set; }

        // Campos para a Autenticação em Duas Etapas (2FA)
        // O Secret é a chave que o App Flutter usará para gerar os códigos
        public string TwoFactorSecret { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string Role { get; set; }
        public int Ativo { get; set; }

        public DateTime CriadoEm { get; set; }
    }
}
