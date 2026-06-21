namespace ProjetoNineGames.Models
{
    public class Dashboard
    {
        public KpiDashboard         Kpis          { get; set; } = new();
        public List<VendaResumo>    UltimasVendas { get; set; } = new();
        public List<JogoTop>        TopJogos      { get; set; } = new();
        public List<ReceitaMensal>  ReceitaMensal { get; set; } = new();
        public List<PagamentoPie>   PorPagamento  { get; set; } = new();
        public List<VendaDetalhe>   DetalheVendas { get; set; } = new();
    }

    public class KpiDashboard
    {
        public decimal ReceitaTotal      { get; set; }
        public int     TotalVendas       { get; set; }
        public decimal TicketMedio       { get; set; }
        public int     TotalClientes     { get; set; }
        public int     TotalJogos        { get; set; }
        public decimal ReceitaMes        { get; set; }
        public decimal ReceitaMesAnterior{ get; set; }

        public double VariacaoMes =>
            ReceitaMesAnterior == 0 ? 100
            : (double)((ReceitaMes - ReceitaMesAnterior) / ReceitaMesAnterior * 100);
    }

    public class VendaResumo
    {
        public int      Id             { get; set; }
        public string   Cliente        { get; set; } = "";
        public string   Email          { get; set; } = "";
        public decimal  ValorTotal     { get; set; }
        public string?  FormaPagamento { get; set; }
        public string   Status         { get; set; } = "";
        public DateTime DataHora       { get; set; }
        public int      QtdItens       { get; set; }
    }

    public class JogoTop
    {
        public int     Id            { get; set; }
        public string  Titulo        { get; set; } = "";
        public string? ImagemUrl     { get; set; }
        public string? Categoria     { get; set; }
        public decimal Preco         { get; set; }
        public int     TotalVendido  { get; set; }
        public decimal ReceitaGerada { get; set; }
        public int     Compradores   { get; set; }
    }

    public class ReceitaMensal
    {
        public string  Mes        { get; set; } = "";
        public string  MesLabel   { get; set; } = "";
        public decimal Receita    { get; set; }
        public int     Quantidade { get; set; }
    }

    public class PagamentoPie
    {
        public string  Forma      { get; set; } = "";
        public int     Quantidade { get; set; }
        public decimal Total      { get; set; }
    }

    public class VendaDetalhe
    {
        public int      VendaId        { get; set; }
        public DateTime DataHora       { get; set; }
        public string   Cliente        { get; set; } = "";
        public string   Email          { get; set; } = "";
        public string   Jogo           { get; set; } = "";
        public string?  Categoria      { get; set; }
        public int      Quantidade     { get; set; }
        public decimal  PrecoUnitario  { get; set; }
        public decimal  Subtotal       { get; set; }
        public string?  FormaPagamento { get; set; }
        public string   Status         { get; set; } = "";
    }
}
