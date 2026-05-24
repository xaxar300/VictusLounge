using System;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge.Services;

public sealed record AuthResult(bool IsSuccess, User? User, string? ErrorMessage);

public sealed class AuthService
{
    public AuthResult Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, null, "Введите логин и пароль.");
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.GetByLogin(login.Trim());
            if (user is null)
            {
                return InvalidCredentials();
            }

            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                if (PasswordHasher.IsHashed(user.PasswordHash)
                    || !string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
                {
                    return InvalidCredentials();
                }

                user.PasswordHash = PasswordHasher.HashPassword(password);
                unitOfWork.SaveChanges();
            }

            return new AuthResult(true, user, null);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, null, $"Не удалось подключиться к SQL Server: {ex.Message}");
        }
    }

    public AuthResult Register(string fullName, string login, string password)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, null, "Заполните имя, логин и пароль.");
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            login = login.Trim();
            if (unitOfWork.Users.Any(user => user.Login == login))
            {
                return new AuthResult(false, null, "Пользователь с таким логином уже есть.");
            }

            var user = new User
            {
                Id = unitOfWork.Users.GetNextId(item => item.Id),
                FullName = fullName.Trim(),
                Login = login,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = "Client",
                Balance = 0m
            };

            unitOfWork.Users.Add(user);
            unitOfWork.SaveChanges();
            return new AuthResult(true, user, null);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, null, $"Не удалось сохранить пользователя в SQL Server: {ex.Message}");
        }
    }

    private static AuthResult InvalidCredentials()
    {
        return new AuthResult(false, null, "Неверный логин или пароль.");
    }
}
