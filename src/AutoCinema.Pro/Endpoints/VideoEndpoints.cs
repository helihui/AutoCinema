using AutoCinema.Pro.Models;
using AutoCinema.Pro.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoCinema.Pro.Endpoints;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/videos");

        // 提交项目
        group.MapPost("/", async (VideoProject project, IProjectService service) =>
        {
            await service.SubmitProjectAsync(project);
            return Results.Accepted($"/api/videos/{project.ProjectId}/progress", project);
        });

        // 获取进度/状态
        group.MapGet("/{id}/progress", (string id, IProjectService service) =>
        {
            var state = service.GetProjectState(id);
            return state is not null ? Results.Ok(state) : Results.NotFound();
        });

        // 获取结果细节
        group.MapGet("/{id}/result", (string id, IProjectService service) =>
        {
            var state = service.GetProjectState(id);
            if (state is null) return Results.NotFound();

            if (state.Status == ProjectStatus.Completed)
            {
                return Results.Ok(new { state.ResultPath, state.FinishedAt });
            }

            return Results.BadRequest(new { state.Status, state.ErrorMessage });
        });

        // 列出所有项目（调试用）
        group.MapGet("/", (IProjectService service) => Results.Ok(service.GetAllProjectStates()));
    }
}
