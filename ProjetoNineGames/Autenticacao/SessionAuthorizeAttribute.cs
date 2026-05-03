using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProjetoNineGames.Autenticacao
{
    /// <summary>
    /// Protege actions exigindo sessão autenticada.
    /// Uso sem parâmetro  → qualquer usuário logado.
    /// Uso com RoleAnyOf  → apenas as roles listadas (CSV).
    /// Exemplo: [SessionAuthorize(RoleAnyOf = "Funcionario,Admin")]
    /// </summary>
    public class SessionAuthorizeAttribute : ActionFilterAttribute
    {
        public string? RoleAnyOf { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http   = context.HttpContext;
            var userId = http.Session.GetInt32(SessionKeys.UserId);

            if (userId == null)
            {
                // Redireciona para login preservando a URL de retorno
                context.Result = new RedirectToActionResult(
                    "Login", "Auth",
                    new { returnUrl = http.Request.Path });
                return;
            }

            if (!string.IsNullOrWhiteSpace(RoleAnyOf))
            {
                var role    = http.Session.GetString(SessionKeys.UserRole) ?? "";
                var allowed = RoleAnyOf.Split(',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);

                if (!allowed.Contains(role))
                {
                    context.Result = new RedirectToActionResult(
                        "AcessoNegado", "Auth", null);
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
