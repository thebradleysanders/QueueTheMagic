using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using XlightsQueue.Data;
using XlightsQueue.DTOs;
using XlightsQueue.Models;

namespace XlightsQueue.Controllers;

[ApiController]
[Route("api/songs")]
public class SongsController(IDbContextFactory<AppDbContext> dbFactory) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> GetSongs() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var playlists = await db.Playlists
            .Where(p => p.IsEnabled)
            .Include(p => p.Songs)
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.FppPlaylistName,
                p.Songs.Select(s => new SongDto(
                    s.Id, s.Title, s.Artist, s.DurationSeconds,
                    s.Ratings.Any() ? Math.Round(s.Ratings.Average(r => r.Rating), 1) : 0,
                    s.Ratings.Count
                )).ToList()
            ))
            .ToListAsync();

        return Ok(playlists);
    }

    [HttpGet("{id}/rating")]
    public async Task<IActionResult> GetRating(int id, [FromQuery] string? sessionToken) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var ratings = await db.SongRatings.Where(r => r.SongId == id).ToListAsync();
        var avg = ratings.Count > 0 ? ratings.Average(r => r.Rating) : 0;
        var yours = sessionToken != null
            ? ratings.FirstOrDefault(r => r.SessionToken == sessionToken)?.Rating
            : null;
        return Ok(new RatingDto(Math.Round(avg, 1), ratings.Count, yours));
    }

    [HttpPost("{id}/rate")]
    public async Task<IActionResult> Rate(int id, [FromBody] RateSongRequest request) {
        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { error = "Rating must be 1–5." });

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.SongRatings
            .FirstOrDefaultAsync(r => r.SongId == id && r.SessionToken == request.SessionToken);

        if (existing != null)
            existing.Rating = request.Rating;
        else
            db.SongRatings.Add(new SongRating { SongId = id, SessionToken = request.SessionToken, Rating = request.Rating });

        await db.SaveChangesAsync();

        var ratings = await db.SongRatings.Where(r => r.SongId == id).ToListAsync();
        return Ok(new RatingDto(Math.Round(ratings.Average(r => r.Rating), 1), ratings.Count, request.Rating));
    }
}
