namespace ProjetoNineGames.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public decimal Preco { get; set; }
        public string ImagemUrl { get; set; }
        public string Categoria { get; set; }
    }
}
