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

        // ── Adicionar ────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Adicionar(int jogoId, int quantidade = 1)
        {
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
            rd.Close();

            var cart      = GetCart();
            var existente = cart.FirstOrDefault(i => i.JogoId == jogoId);
            if (existente != null) existente.Quantidade += quantidade;
            else                   cart.Add(jogo);

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
                else                 item.Quantidade = quantidade;
            }
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        // ── Remover ───────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Remover(int jogoId)
        {
            var cart = GetCart();
            cart.RemoveAll(i => i.JogoId == jogoId);
            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        // ── CHECKOUT — GET (exibe tela de pagamento) ─────────────────────────

        [SessionAuthorize]
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["erro"] = "Seu carrinho está vazio.";
                return RedirectToAction(nameof(Index));
            }

            // Gera chave Pix fictícia (em produção viria do gateway)
            var pixChave = "00020126580014BR.GOV.BCB.PIX0136" +
                           Guid.NewGuid().ToString("N")[..32] +
                           "5204000053039865406" +
                           cart.Sum(i => i.Subtotal).ToString("F2").Replace(",", ".") +
                           "5802BR5913NineGames6008SaoPaulo62070503***6304";

            var vm = new Pagamento
            {
                Total        = cart.Sum(i => i.Subtotal),
                Itens        = cart,
                PixChave     = "ninegames@pagamentos.com.br",
                PixQrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(pixChave)}"
            };

            return View(vm);
        }

        // ── CHECKOUT — POST (processa e grava a venda) ───────────────────────

        [SessionAuthorize]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Checkout(Pagamento vm)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["erro"] = "Seu carrinho está vazio.";
                return RedirectToAction(nameof(Index));
            }

            // Validação dos campos do cartão
            if (vm.FormaPagamento is "Crédito" or "Débito")
            {
                if (string.IsNullOrWhiteSpace(vm.NomeTitular))
                    ModelState.AddModelError("NomeTitular", "Informe o nome do titular.");

                if (string.IsNullOrWhiteSpace(vm.NumeroCartao) || vm.NumeroCartao.Replace(" ", "").Length < 16)
                    ModelState.AddModelError("NumeroCartao", "Número do cartão inválido.");

                if (string.IsNullOrWhiteSpace(vm.Validade) || !System.Text.RegularExpressions.Regex.IsMatch(vm.Validade, @"^\d{2}/\d{2}$"))
                    ModelState.AddModelError("Validade", "Validade inválida (MM/AA).");

                if (string.IsNullOrWhiteSpace(vm.Cvv) || vm.Cvv.Length < 3)
                    ModelState.AddModelError("Cvv", "CVV inválido.");

                if (!ModelState.IsValid)
                {
                    vm.Total = cart.Sum(i => i.Subtotal);
                    vm.Itens = cart;
                    return View(vm);
                }
            }

            // Grava a venda no banco
            var idUsuario = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var total     = cart.Sum(i => i.Subtotal);
            var formaPgto = vm.FormaPagamento +
                            (vm.FormaPagamento == "Crédito" && vm.Parcelas > 1
                                ? $" {vm.Parcelas}x"
                                : "");

            using var conn = _db.GetConnection();
            using var tx   = conn.BeginTransaction();

            try
            {
                int idVenda;
                using (var cmd = new MySqlCommand("sp_venda_criar", conn, tx)
                                 { CommandType = CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("p_id_usuario",     idUsuario);
                    cmd.Parameters.AddWithValue("p_valor_total",    total);
                    cmd.Parameters.AddWithValue("p_forma_pagamento", formaPgto);
                    var pOut = new MySqlParameter("p_id_gerado", MySqlDbType.Int32)
                               { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pOut);
                    cmd.ExecuteNonQuery();
                    idVenda = Convert.ToInt32(pOut.Value);
                }

                foreach (var item in cart)
                {
                    using var cmdI = new MySqlCommand("sp_venda_adicionar_item", conn, tx)
                                    { CommandType = CommandType.StoredProcedure };
                    cmdI.Parameters.AddWithValue("p_id_venda",      idVenda);
                    cmdI.Parameters.AddWithValue("p_id_jogo",       item.JogoId);
                    cmdI.Parameters.AddWithValue("p_quantidade",    item.Quantidade);
                    cmdI.Parameters.AddWithValue("p_preco_unitario", item.PrecoUnitario);
                    cmdI.ExecuteNonQuery();
                }

                using (var cmdF = new MySqlCommand("sp_venda_finalizar", conn, tx)
                                  { CommandType = CommandType.StoredProcedure })
                {
                    cmdF.Parameters.AddWithValue("p_id_venda", idVenda);
                    cmdF.ExecuteNonQuery();
                }

                tx.Commit();
                HttpContext.Session.Remove(CART_KEY);
                TempData["ok"]      = $"Compra #{idVenda} realizada com sucesso! 🎮";
                TempData["idVenda"] = idVenda;
                return RedirectToAction(nameof(Confirmacao));
            }
            catch (MySqlException ex)
            {
                tx.Rollback();
                TempData["erro"] = $"Falha ao finalizar: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ── Confirmação ───────────────────────────────────────────────────────

        [SessionAuthorize]
        [HttpGet]
        public IActionResult Confirmacao()
        {
            if (TempData["ok"] == null)
                return RedirectToAction(nameof(Index));

            ViewBag.Mensagem = TempData["ok"];
            ViewBag.IdVenda  = TempData["idVenda"];
            return View();
        }
    }
}
