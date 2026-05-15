using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;
using System.Text.Json;

namespace ProjetoNineGames.Controllers
{
    public class CarrinhoController : Controller
    {
        private readonly Database _db = new();
        private const string CART_KEY = "Carrinho_NG";

        // ── Helpers de sessão ────────────────────────────────────────────────

        private List<ItemCarrinho> GetCart()
        {
            var json = HttpContext.Session.GetString(CART_KEY);
            if (string.IsNullOrEmpty(json)) return new();
            return JsonSerializer.Deserialize<List<ItemCarrinho>>(json) ?? new();
        }

        private void SaveCart(List<ItemCarrinho> itens)
        {
            if (itens.Count == 0) { HttpContext.Session.Remove(CART_KEY); return; }
            HttpContext.Session.SetString(CART_KEY, JsonSerializer.Serialize(itens));
        }

        // ── Adicionar ao carrinho (POST — chamado da vitrine/detalhe) ────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Adicionar(int jogoId, int quantidade = 1)
        {
            // Busca dados do jogo no banco para garantir preço correto
            using var conn = _db.GetConnection();
            using var cmd  = new MySqlCommand("sp_jogo_obter", conn)
                             { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_id", jogoId);
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
            {
                TempData["erro"] = "Jogo não encontrado.";
                return RedirectToAction("Index", "Jogo");
            }

            var jogo = new ItemCarrinho
            {
                JogoId        = rd.GetInt32("id"),
                Titulo        = rd.GetString("titulo"),
                ImagemUrl     = rd["imagem_url"] as string,
                PrecoUnitario = rd.GetDecimal("preco"),
                Quantidade    = quantidade
            };
            var estoque = rd.GetInt32("estoque");
            rd.Close();

            if (estoque <= 0)
            {
                TempData["erro"] = "Jogo fora de estoque.";
                return RedirectToAction("Index", "Jogo");
            }

            var cart = GetCart();
            var existente = cart.FirstOrDefault(i => i.JogoId == jogoId);
            if (existente != null)
                existente.Quantidade = Math.Min(existente.Quantidade + quantidade, estoque);
            else
                cart.Add(jogo);

            SaveCart(cart);
            TempData["ok"] = $"\"{jogo.Titulo}\" adicionado ao carrinho.";
            return RedirectToAction("Index", "Jogo");
        }

        // ── Ver carrinho ─────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            return View(GetCart());
        }

        // ── Alterar quantidade ────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AlterarQtd(int jogoId, int quantidade)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i => i.JogoId == jogoId);
            if (item != null)
            {
                if (quantidade <= 0) cart.Remove(item);
                else item.Quantidade = quantidade;
            }
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        // ── Remover item ──────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Remover(int jogoId)
        {
            var cart = GetCart();
            cart.RemoveAll(i => i.JogoId == jogoId);
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        // ── Finalizar compra ─────────────────────────────────────────────────
        // Requer login. Grava venda + itens em transação.

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Finalizar(string formaPagamento)
        {
            var cart = GetCart();
            if (cart.Count == 0)
            {
                TempData["erro"] = "Seu carrinho está vazio.";
                return RedirectToAction(nameof(Index));
            }

            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var total     = cart.Sum(i => i.Subtotal);

            using var conn = _db.GetConnection();
            using var tx   = conn.BeginTransaction();

            try
            {
                // 1) Cria cabeçalho da venda
                int idVenda;
                using (var cmd = new MySqlCommand("sp_venda_criar", conn, tx)
                                 { CommandType = CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("p_id_usuario",       idUsuario);
                    cmd.Parameters.AddWithValue("p_valor_total",       total);
                    cmd.Parameters.AddWithValue("p_forma_pagamento",   (object?)formaPagamento ?? DBNull.Value);
                    var pOut = new MySqlParameter("p_id_gerado", MySqlDbType.Int32)
                               { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pOut);
                    cmd.ExecuteNonQuery();
                    idVenda = Convert.ToInt32(pOut.Value);
                }

                // 2) Insere cada item + baixa estoque via SP
                foreach (var item in cart)
                {
                    using var cmdI = new MySqlCommand("sp_venda_adicionar_item", conn, tx)
                                    { CommandType = CommandType.StoredProcedure };
                    cmdI.Parameters.AddWithValue("p_id_venda",       idVenda);
                    cmdI.Parameters.AddWithValue("p_id_jogo",        item.JogoId);
                    cmdI.Parameters.AddWithValue("p_quantidade",     item.Quantidade);
                    cmdI.Parameters.AddWithValue("p_preco_unitario", item.PrecoUnitario);
                    cmdI.ExecuteNonQuery();
                }

                // 3) Finaliza venda
                using (var cmdF = new MySqlCommand("sp_venda_finalizar", conn, tx)
                                  { CommandType = CommandType.StoredProcedure })
                {
                    cmdF.Parameters.AddWithValue("p_id_venda", idVenda);
                    cmdF.ExecuteNonQuery();
                }

                tx.Commit();
                HttpContext.Session.Remove(CART_KEY);
                TempData["ok"] = $"Compra #{idVenda} realizada com sucesso! 🎮";
                return RedirectToAction("MinhaBiblioteca", "Biblioteca");
            }
            catch (MySqlException ex)
            {
                tx.Rollback();
                TempData["erro"] = $"Falha ao finalizar: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
