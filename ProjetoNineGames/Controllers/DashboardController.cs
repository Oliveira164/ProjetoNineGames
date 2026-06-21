using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ProjetoNineGames.Autenticacao;
using ProjetoNineGames.Data;
using ProjetoNineGames.Models;
using System.Data;

namespace ProjetoNineGames.Controllers
{
    [SessionAuthorize(RoleAnyOf = "Admin,Funcionario")]
    public class DashboardController : Controller
    {
        private readonly Database _db = new();

        [HttpGet]
        public IActionResult Index()
        {
            var vm = new Dashboard();
            using var conn = _db.GetConnection();

            // ── KPIs ────────────────────────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_kpis", conn)
                             { CommandType = CommandType.StoredProcedure })
            using (var rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    vm.Kpis = new KpiDashboard
                    {
                        ReceitaTotal       = rd.GetDecimal("receita_total"),
                        TotalVendas        = rd.GetInt32("total_vendas"),
                        TicketMedio        = rd.GetDecimal("ticket_medio"),
                        TotalClientes      = rd.GetInt32("total_clientes"),
                        TotalJogos         = rd.GetInt32("total_jogos"),
                        ReceitaMes         = rd.GetDecimal("receita_mes"),
                        ReceitaMesAnterior = rd.GetDecimal("receita_mes_anterior"),
                    };
                }
            }

            // ── Últimas vendas ───────────────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_ultimas_vendas", conn)
                             { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("p_limite", 10);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    vm.UltimasVendas.Add(new VendaResumo
                    {
                        Id             = rd.GetInt32("id"),
                        Cliente        = rd.GetString("cliente"),
                        Email          = rd.GetString("email"),
                        ValorTotal     = rd.GetDecimal("valor_total"),
                        FormaPagamento = rd["forma_pagamento"] as string,
                        Status         = rd.GetString("status"),
                        DataHora       = rd.GetDateTime("data_hora"),
                        QtdItens       = rd.GetInt32("qtd_itens"),
                    });
                }
            }

            // ── Top jogos ────────────────────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_top_jogos", conn)
                             { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("p_limite", 8);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    vm.TopJogos.Add(new JogoTop
                    {
                        Id            = rd.GetInt32("id"),
                        Titulo        = rd.GetString("titulo"),
                        ImagemUrl     = rd["imagem_url"] as string,
                        Categoria     = rd["categoria"]  as string,
                        Preco         = rd.GetDecimal("preco"),
                        TotalVendido  = Convert.ToInt32(rd["total_vendido"]),
                        ReceitaGerada = rd.GetDecimal("receita_gerada"),
                        Compradores   = Convert.ToInt32(rd["compradores"]),
                    });
                }
            }

            // ── Receita mensal ───────────────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_receita_mensal", conn)
                             { CommandType = CommandType.StoredProcedure })
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    vm.ReceitaMensal.Add(new ReceitaMensal
                    {
                        Mes        = rd.GetString("mes"),
                        MesLabel   = rd.GetString("mes_label"),
                        Receita    = rd.GetDecimal("receita"),
                        Quantidade = rd.GetInt32("quantidade"),
                    });
                }
            }

            // ── Por forma de pagamento ───────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_por_pagamento", conn)
                             { CommandType = CommandType.StoredProcedure })
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    vm.PorPagamento.Add(new PagamentoPie
                    {
                        Forma      = rd.GetString("forma"),
                        Quantidade = rd.GetInt32("quantidade"),
                        Total      = rd.GetDecimal("total"),
                    });
                }
            }

            // ── Detalhe de vendas ────────────────────────────────────────────
            using (var cmd = new MySqlCommand("sp_dashboard_vendas_detalhe", conn)
                             { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("p_limite", 50);
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    vm.DetalheVendas.Add(new VendaDetalhe
                    {
                        VendaId        = rd.GetInt32("venda_id"),
                        DataHora       = rd.GetDateTime("data_hora"),
                        Cliente        = rd.GetString("cliente"),
                        Email          = rd.GetString("email"),
                        Jogo           = rd.GetString("jogo"),
                        Categoria      = rd["categoria"]       as string,
                        Quantidade     = rd.GetInt32("quantidade"),
                        PrecoUnitario  = rd.GetDecimal("preco_unitario"),
                        Subtotal       = rd.GetDecimal("subtotal"),
                        FormaPagamento = rd["forma_pagamento"] as string,
                        Status         = rd.GetString("status"),
                    });
                }
            }

            return View(vm);
        }
    }
}
