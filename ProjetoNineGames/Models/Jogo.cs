namespace ProjetoNineGames.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public decimal Preco { get; set; }
        public string ImagemUrl { get; set; }

        // Mapeia a coluna id_categoria (Foreign Key)
        public int? IdCategoria { get; set; }

        // Mapeia o c.nome retornado pelos JOINs nas Stored Procedures
        public string Categoria { get; set; }

        // Mapeia a coluna criado_em
        public DateTime CriadoEm { get; set; }
    }
}
