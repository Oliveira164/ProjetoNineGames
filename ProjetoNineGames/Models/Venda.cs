namespace ProjetoNineGames.Models
{
    public class Venda
    {
        public int      Id             { get; set; }
        public int      IdUsuario      { get; set; }
        public string   NomeUsuario    { get; set; } = "";
        public DateTime DataHora       { get; set; }
        public decimal  ValorTotal     { get; set; }
        public string?  FormaPagamento { get; set; }
        public string   Status         { get; set; } = "Aberta";
        public List<VendaItem> Itens   { get; set; } = new();
    }

    public class VendaItem
    {
        public int     Id            { get; set; }
        public int     IdVenda       { get; set; }
        public int     IdJogo        { get; set; }
        public string  TituloJogo    { get; set; } = "";
        public string? ImagemUrl     { get; set; }
        public int     Quantidade    { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
