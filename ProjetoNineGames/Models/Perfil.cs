namespace ProjetoNineGames.Models
{
    public class Perfil
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string? FotoUrl { get; set; }
        public string? Telefone { get; set; }
        public string? DataNasc { get; set; }   // "yyyy-MM-dd" para o input date
        public string? Cpf { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTime CriadoEm { get; set; }
    }

    public class Cartao
    {
        public int Id { get; set; }
        public string Bandeira { get; set; } = "";
        public string Ultimos4 { get; set; } = "";
        public string NomeTitular { get; set; } = "";
        public string Validade { get; set; } = "";
        public bool Principal { get; set; }
        public DateTime CriadoEm { get; set; }

        // Ícone por bandeira
        public string Icone => Bandeira switch
        {
            "Visa" => "🔵",
            "Mastercard" => "🔴",
            "Amex" => "🟢",
            "Elo" => "⚫",
            "Hipercard" => "🟣",
            _ => "💳"
        };
    }

    public class Pedido
    {
        public int Id { get; set; }
        public DateTime DataHora { get; set; }
        public decimal ValorTotal { get; set; }
        public string? FormaPagamento { get; set; }
        public string Status { get; set; } = "";
        public int QtdItens { get; set; }
        public List<PedidoItem> Itens { get; set; } = new();
    }

    public class PedidoItem
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string? ImagemUrl { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
