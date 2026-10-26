using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Api.Auth;
using PixelPay.Application.Common;
using PixelPay.Application.Wallets;

namespace PixelPay.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/wallet")]
public class WalletController(WalletService wallet) : ControllerBase
{
    [HttpGet]
    public Task<WalletWithTransactions> Get(CancellationToken ct) => wallet.GetWalletAsync(HttpContext.GetCurrentUser().Id, ct);

    // Raw query values: Nest's defaults apply only when the key is absent, and parseInt("") is NaN.
    [HttpGet("transactions")]
    public Task<TransactionsPage> Transactions(CancellationToken ct) =>
        wallet.GetTransactionsAsync(HttpContext.GetCurrentUser().Id, Js.ParseInt(Query("page", "1")), Js.ParseInt(Query("limit", "20")), ct);

    [HttpPost("deposit")]
    [Authorize(Roles = "ADMIN")]
    public Task<DepositResponse> Deposit(DepositDto dto, CancellationToken ct) => wallet.AdminDepositAsync(dto, ct);

    private string Query(string key, string fallback) => Request.Query.TryGetValue(key, out var v) ? v.ToString() : fallback;
}
