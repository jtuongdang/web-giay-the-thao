using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QuanLyBanGiay.Models;

namespace QuanLyBanGiay.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index(string? brand, string? search)
    {
        var shoes = ShoeDataService.GetAll();

        if (!string.IsNullOrWhiteSpace(brand) && !brand.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            shoes = shoes.Where(s => s.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            shoes = shoes.Where(s => s.Name.ToLower().Contains(keyword) || 
                                     s.Brand.ToLower().Contains(keyword) || 
                                     s.Category.ToLower().Contains(keyword)).ToList();
        }

        ViewBag.CurrentBrand = brand ?? "All";
        ViewBag.SearchQuery = search ?? "";
        return View(shoes);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
