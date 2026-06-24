using System.ComponentModel.DataAnnotations;

namespace ProjetoNineGames.Models
{
    /// <summary>
    /// ViewModel do checkout. Contém os dados de pagamento
    /// que o usuário preenche na tela de finalização.
    /// </summary>
    public class Pagamento
    {
        // ── Método escolhido ─────────────────────────────────────────────────
        [Required(ErrorMessage = "Selecione a forma de pagamento.")]
        public string FormaPagamento { get; set; } = "";

        // ── Cartão (crédito ou débito) ───────────────────────────────────────
        public string? NomeTitular    { get; set; }
        public string? NumeroCartao   { get; set; }   // apenas exibição mascarada
        public string? Validade       { get; set; }   // MM/AA
        public string? Cvv            { get; set; }
        public int?    Parcelas       { get; set; }   // apenas para crédito

        // ── Pix ──────────────────────────────────────────────────────────────
        // Gerado pelo servidor — não precisa de input do usuário
        public string? PixChave       { get; set; }
        public string? PixQrCodeUrl   { get; set; }

        // ── Resumo (preenchido pelo controller) ───────────────────────────────
        public decimal Total          { get; set; }
        public List<ItemCarrinho> Itens { get; set; } = new();

        public bool SalvarCartao { get; set; }
        public List<Cartao> CartoesSalvos { get; set; } = new List<Cartao>();
    }
}
