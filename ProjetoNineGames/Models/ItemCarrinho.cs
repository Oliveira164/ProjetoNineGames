namespace ProjetoNineGames.Models
{
    /// <summary>
    /// Representa um item do carrinho mantido em sessão (JSON).
    /// </summary>
    public class ItemCarrinho
    {
        public int     JogoId        { get; set; }
        public string  Titulo        { get; set; } = "";
        public string? ImagemUrl     { get; set; }
        public decimal PrecoUnitario { get; set; }
        public int     Quantidade    { get; set; }
        public decimal Subtotal      => PrecoUnitario * Quantidade;
    }
}
