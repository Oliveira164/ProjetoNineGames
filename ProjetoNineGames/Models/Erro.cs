namespace ProjetoNineGames.Models
{
    public class Erro
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
