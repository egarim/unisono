using Pupuseria.Services;
using Unisono;

namespace Pupuseria.Tools;

/// <summary>How Siri and Shortcuts resolve a "Pupusa": by id, by text ("the one with loroco"), or from the suggested list.</summary>
public sealed class PupusaEntityQuery(IOrderService kitchen) : IEntityQuery<PupusaDto>
{
	public ValueTask<IReadOnlyList<PupusaDto>> ByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
		=> new(ids.Select(kitchen.FindPupusa).Where(p => p is not null).Select(p => PupusaDto.From(p!)).ToList());

	public ValueTask<IReadOnlyList<PupusaDto>> SearchAsync(string text, CancellationToken cancellationToken = default)
		=> new(kitchen.SearchPupusas(text).Select(PupusaDto.From).ToList());

	public ValueTask<IReadOnlyList<PupusaDto>> SuggestedAsync(CancellationToken cancellationToken = default)
		=> new(kitchen.Menu.Select(PupusaDto.From).ToList());
}
