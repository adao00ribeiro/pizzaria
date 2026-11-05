using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PizzariaLucrativa.Application.Interfaces;

namespace PizzariaLucrativa.Api.Extensions;

/// <summary>
/// Manipulador do comando administrativo de recuperação break-glass (--reset-system-admin).
/// </summary>
public static class SystemAdminCliHandler
{
    public static async Task<bool> TryHandleCliAsync(IServiceProvider services, string[] args)
    {
        if (!args.Contains("--reset-system-admin", StringComparer.OrdinalIgnoreCase))
            return false;

        string? email = null;
        string? password = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--email", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                email = args[i + 1];
            }
            else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                password = args[i + 1];
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            email = "admin@plataforma.com";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Write("Digite a nova senha do Administrador do Sistema: ");
            password = ReadPasswordMasked();
            Console.WriteLine();
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] A senha deve ter no mínimo 8 caracteres.");
            Console.ResetColor();
            Environment.Exit(1);
            return true;
        }

        using var scope = services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<ISystemAdminService>();

        await adminService.ResetPasswordCliAsync(email, password);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCESSO] Credenciais do Administrador do Sistema ({email}) atualizadas com sucesso!");
        Console.ResetColor();

        return true;
    }

    private static string ReadPasswordMasked()
    {
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
                Console.Write("*");
            }
        }
        return sb.ToString();
    }
}
