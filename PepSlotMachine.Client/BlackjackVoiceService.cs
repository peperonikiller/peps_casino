using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.Resources;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace PepSlotMachine
{
    internal static class BlackjackVoiceService
    {
        private static readonly EPhraseTrigger[] LossTaunts =
        {
            EPhraseTrigger.Toxic,
            EPhraseTrigger.Provocation,
            EPhraseTrigger.BadWork,
            EPhraseTrigger.Negative,
            EPhraseTrigger.OnMutter
        };

        private static readonly System.Random Random =
            new System.Random();

        private static string _lastLossKey =
            string.Empty;

        private static readonly HashSet<string> SeenTableEvents =
            new HashSet<string>(
                StringComparer.Ordinal);

        internal static void ProcessTableTaunts(
            MonoBehaviour coroutineHost,
            InventoryController controller,
            BlackjackRoomState room)
        {
            if (coroutineHost == null ||
                controller == null ||
                room == null ||
                room.TauntEvents == null)
            {
                return;
            }

            foreach (BlackjackTauntEvent taunt
                in room.TauntEvents)
            {
                if (taunt == null ||
                    string.IsNullOrEmpty(
                        taunt.EventId))
                {
                    continue;
                }

                if (!SeenTableEvents.Add(
                        taunt.EventId))
                {
                    continue;
                }

                float delay =
                    Mathf.Clamp(
                        (float)taunt.DelaySeconds,
                        0f,
                        4f);
coroutineHost.StartCoroutine(
                    PlayTableTauntAfterDelay(
                        taunt,
                        delay));
            }

            // Avoid an unbounded client-side id set over very long sessions.
            if (SeenTableEvents.Count > 512)
            {
                SeenTableEvents.Clear();

                foreach (BlackjackTauntEvent taunt
                    in room.TauntEvents)
                {
                    if (taunt != null &&
                        !string.IsNullOrEmpty(
                            taunt.EventId))
                    {
                        SeenTableEvents.Add(
                            taunt.EventId);
                    }
                }
            }
        }

        internal static void TryPlayLossTaunt(
            InventoryController controller,
            BlackjackRoomState room)
        {
            // Kept only for source compatibility with older call sites.
            // Phase 12E uses ProcessTableTaunts() so every client at the
            // active table consumes the same synchronized server events.
        }

        private static System.Collections.IEnumerator PlayTableTauntAfterDelay(
            BlackjackTauntEvent taunt,
            float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    delay);
            }

            PlaySynchronizedTaunt(
                taunt);
        }

        private static void PlaySynchronizedTaunt(
            BlackjackTauntEvent taunt)
        {
            bool isWin =
                string.Equals(
                    taunt.Kind,
                    "WIN",
                    StringComparison.OrdinalIgnoreCase);

            EPhraseTrigger phrase;

            if (!Enum.TryParse(
                    taunt.Phrase,
                    true,
                    out phrase))
            {
                phrase =
                    isWin
                        ? EPhraseTrigger.GoodWork
                        : EPhraseTrigger.Toxic;
            }

            string voiceName =
                string.IsNullOrWhiteSpace(
                    taunt.VoiceName)
                    ? "Usec_1"
                    : taunt.VoiceName;

            try
            {
                Voice voice =
                    Singleton<IEasyAssets>.Instance
                        .GetAsset<Voice>(
                            InGameBundles.TakePhrasePath(
                                voiceName));

                if (voice == null ||
                    voice.Banks == null)
                {
                    Plugin.Log?.LogWarning(
                        $"Table taunt {taunt.EventId}: voice {voiceName} could not be loaded.");

                    return;
                }

                TagBank bank =
                    voice.Banks
                        .FirstOrDefault(
                            x =>
                                x != null &&
                                x.Trigger ==
                                phrase &&
                                x.Clips != null &&
                                x.Clips.Length > 0);

                // Not every EFT voice contains every phrase bank. For example,
                // Usec_1 may not contain Toxic. Fall back deterministically to
                // another casino-appropriate bank that THIS voice actually has.
                // Because EventId and the ordered candidate set are identical
                // on every table client, all clients still select the same phrase.
                if (bank == null)
                {
                    EPhraseTrigger[] fallbackTriggers =
                        isWin
                            ? new[]
                            {
                                EPhraseTrigger.GoodWork,
                                EPhraseTrigger.OnGoodWork,
                                EPhraseTrigger.Ready,
                                EPhraseTrigger.Roger,
                                EPhraseTrigger.Greetings
                            }
                            : new[]
                            {
                                EPhraseTrigger.Provocation,
                                EPhraseTrigger.BadWork,
                                EPhraseTrigger.Negative,
                                EPhraseTrigger.OnMutter,
                                EPhraseTrigger.Toxic
                            };

                    TagBank[] playable =
                        fallbackTriggers
                            .Select(
                                trigger =>
                                    voice.Banks.FirstOrDefault(
                                        x =>
                                            x != null &&
                                            x.Trigger == trigger &&
                                            x.Clips != null &&
                                            x.Clips.Length > 0))
                            .Where(
                                x =>
                                    x != null)
                            .ToArray();

                    if (playable.Length == 0)
                    {
                        Plugin.Log?.LogWarning(
                            $"Table taunt {taunt.EventId}: voice {voiceName} has none of the casino taunt banks.");

                        return;
                    }

                    int fallbackIndex =
                        PositiveStableHash(
                            taunt.EventId + "|bank") %
                        playable.Length;

                    bank =
                        playable[fallbackIndex];

                    phrase =
                        bank.Trigger;

                    Plugin.Log?.LogInfo(
                        $"Table taunt {taunt.EventId}: requested bank {taunt.Phrase} unavailable on {voiceName}; using {phrase}.");
                }

                // Use the event id for deterministic clip selection so every
                // table client chooses the same clip from the same native bank.
                int index =
                    PositiveStableHash(
                        taunt.EventId + "|" + phrase) %
                    bank.Clips.Length;

                TaggedClip taggedClip =
                    bank.Clips[index];

                AudioClip clip =
                    taggedClip?.Clip;

                if (clip == null)
                {
                    return;
                }

                GUISounds guiSounds =
                    ResolveGuiSounds();

                if (guiSounds == null)
                {
                    return;
                }

                float volume =
                    taggedClip.Volume > 0f
                        ? taggedClip.Volume
                        : 1f;

                guiSounds.PlaySound(
                    clip,
                    single: false,
                    commonUiSound: true,
                    volume: volume);
}
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Synchronized Blackjack table taunt failed ({taunt.EventId}): {ex.Message}");
            }
        }

        private static int PositiveStableHash(
            string value)
        {
            unchecked
            {
                int hash =
                    23;

                foreach (char c
                    in value ?? string.Empty)
                {
                    hash =
                        hash * 31 +
                        c;
                }

                return hash == int.MinValue
                    ? int.MaxValue
                    : Math.Abs(hash);
            }
        }

        internal static void PlayLocalSlotWinCelebration(
            InventoryController controller)
        {
            if (controller == null)
            {
                return;
            }

            EPhraseTrigger[] celebration =
            {
                EPhraseTrigger.GoodWork,
                EPhraseTrigger.OnGoodWork,
                EPhraseTrigger.Ready,
                EPhraseTrigger.Roger,
                EPhraseTrigger.Greetings
            };

            string side =
                TryGetProfileSide(
                    controller);

            string[] voiceNames =
                string.Equals(
                    side,
                    "Bear",
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { "Bear_1", "Usec_1" }
                    : new[] { "Usec_1", "Bear_1" };

            foreach (string voiceName
                in voiceNames)
            {
                try
                {
                    Voice voice =
                        Singleton<IEasyAssets>.Instance
                            .GetAsset<Voice>(
                                InGameBundles.TakePhrasePath(
                                    voiceName));

                    if (voice?.Banks == null)
                    {
                        continue;
                    }

                    EPhraseTrigger[] shuffled =
                        celebration
                            .OrderBy(
                                _ =>
                                    Random.Next())
                            .ToArray();

                    foreach (EPhraseTrigger phrase
                        in shuffled)
                    {
                        TagBank bank =
                            voice.Banks
                                .FirstOrDefault(
                                    x =>
                                        x != null &&
                                        x.Trigger == phrase &&
                                        x.Clips != null &&
                                        x.Clips.Length > 0);

                        if (bank == null)
                        {
                            continue;
                        }

                        TaggedClip taggedClip =
                            bank.Clips[
                                Random.Next(
                                    bank.Clips.Length)];

                        AudioClip clip =
                            taggedClip?.Clip;

                        GUISounds guiSounds =
                            ResolveGuiSounds();

                        if (clip == null ||
                            guiSounds == null)
                        {
                            continue;
                        }

                        float volume =
                            taggedClip.Volume > 0f
                                ? taggedClip.Volume
                                : 1f;

                        // Local-only: this is intentionally not put into the
                        // shared Blackjack room event stream.
                        guiSounds.PlaySound(
                            clip,
                            single: true,
                            commonUiSound: true,
                            volume: volume);

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Slot win celebration voice failed ({voiceName}): {ex.Message}");
                }
            }
        }

        private static string BuildLossKey(
            BlackjackRoomState room,
            BlackjackPlayerState local)
        {
            if (room.ResolvedUtc.HasValue)
            {
                return room.RoomId +
                    "|" +
                    room.ResolvedUtc.Value
                        .ToUniversalTime()
                        .Ticks;
            }

            return room.RoomId +
                "|" +
                local.Wager +
                "|" +
                string.Join(
                    ",",
                    local.Cards?
                        .Select(
                            c =>
                                (c?.Rank ?? string.Empty) +
                                (c?.Suit ?? string.Empty))
                    ?? Array.Empty<string>());
        }

        private static BaseSpeaker FindPreviewSpeaker(
            EPhraseTrigger[] preferredTriggers)
        {
            try
            {
                // Character-screen speaker instances are owned by Unity menu /
                // preview objects rather than existing as UnityEngine.Object
                // themselves. Search loaded behaviours/components and inspect
                // their fields/properties for a BaseSpeaker reference.
                UnityEngine.Object[] objects =
                    Resources.FindObjectsOfTypeAll<
                        UnityEngine.Object>();

                var visited =
                    new HashSet<BaseSpeaker>();

                foreach (UnityEngine.Object obj
                    in objects)
                {
                    if (obj == null)
                    {
                        continue;
                    }

                    foreach (BaseSpeaker speaker
                        in GetSpeakersFromObject(
                            obj))
                    {
                        if (speaker == null ||
                            !visited.Add(
                                speaker))
                        {
                            continue;
                        }

                        if (IsUsableSpeaker(
                                speaker,
                                preferredTriggers))
                        {
                            Plugin.Log?.LogInfo(
                                $"Found Character preview speaker: voice={speaker.PlayerVoice}, banks={speaker.PhrasesBanks.Count}");

                            return speaker;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Preview speaker lookup failed: {ex.Message}");
            }

            return null;
        }

        private static IEnumerable<BaseSpeaker> GetSpeakersFromObject(
            UnityEngine.Object owner)
        {
            Type type =
                owner.GetType();

            BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            FieldInfo[] fields;

            try
            {
                fields =
                    type.GetFields(
                        flags);
            }
            catch
            {
                fields =
                    Array.Empty<FieldInfo>();
            }

            foreach (FieldInfo field
                in fields)
            {
                if (!typeof(BaseSpeaker)
                    .IsAssignableFrom(
                        field.FieldType))
                {
                    continue;
                }

                BaseSpeaker speaker =
                    null;

                try
                {
                    speaker =
                        field.GetValue(
                            owner)
                        as BaseSpeaker;
                }
                catch
                {
                }

                if (speaker != null)
                {
                    yield return speaker;
                }
            }

            PropertyInfo[] properties;

            try
            {
                properties =
                    type.GetProperties(
                        flags);
            }
            catch
            {
                properties =
                    Array.Empty<PropertyInfo>();
            }

            foreach (PropertyInfo property
                in properties)
            {
                if (!property.CanRead ||
                    property.GetIndexParameters()
                        .Length != 0 ||
                    !typeof(BaseSpeaker)
                        .IsAssignableFrom(
                            property.PropertyType))
                {
                    continue;
                }

                BaseSpeaker speaker =
                    null;

                try
                {
                    speaker =
                        property.GetValue(
                            owner)
                        as BaseSpeaker;
                }
                catch
                {
                }

                if (speaker != null)
                {
                    yield return speaker;
                }
            }
        }

        private static bool IsUsableSpeaker(
            BaseSpeaker speaker,
            EPhraseTrigger[] triggers)
        {
            if (speaker.PhrasesBanks == null ||
                speaker.PhrasesBanks.Count == 0 ||
                string.IsNullOrEmpty(
                    speaker.PlayerVoice))
            {
                return false;
            }

            return triggers.Any(
                trigger =>
                    speaker.PhrasesBanks.ContainsKey(
                        trigger));
        }

        private static bool TryPlayStockVoiceFallback(
            InventoryController controller,
            EPhraseTrigger[] shuffled)
        {
            string side =
                TryGetProfileSide(
                    controller);

            string[] voiceNames;

            if (string.Equals(
                    side,
                    "Bear",
                    StringComparison.OrdinalIgnoreCase))
            {
                voiceNames =
                    new[]
                    {
                        "Bear_1",
                        "Usec_1"
                    };
            }
            else if (string.Equals(
                         side,
                         "Usec",
                         StringComparison.OrdinalIgnoreCase))
            {
                voiceNames =
                    new[]
                    {
                        "Usec_1",
                        "Bear_1"
                    };
            }
            else
            {
                // We could not determine faction from the menu profile.
                // Try EFT's two stock PMC defaults. We deliberately do not
                // use Scav_1 for Pep's Casino PMC Blackjack.
                voiceNames =
                    new[]
                    {
                        "Usec_1",
                        "Bear_1"
                    };
            }

            foreach (string voiceName
                in voiceNames)
            {
                try
                {
                    Voice voice =
                        Singleton<IEasyAssets>.Instance
                            .GetAsset<Voice>(
                                InGameBundles.TakePhrasePath(
                                    voiceName));

                    if (voice == null ||
                        voice.Banks == null)
                    {
                        continue;
                    }

                    foreach (EPhraseTrigger phrase
                        in shuffled)
                    {
                        TagBank bank =
                            voice.Banks
                                .FirstOrDefault(
                                    x =>
                                        x != null &&
                                        x.Trigger ==
                                        phrase);

                        if (bank == null ||
                            bank.Clips == null ||
                            bank.Clips.Length == 0)
                        {
                            continue;
                        }

                        TaggedClip taggedClip =
                            bank.Clips[
                                Random.Next(
                                    bank.Clips.Length)];

                        AudioClip clip =
                            taggedClip?.Clip;

                        if (clip == null)
                        {
                            continue;
                        }

                        GUISounds guiSounds =
                            ResolveGuiSounds();

                        if (guiSounds == null)
                        {
                            return false;
                        }

                        float volume =
                            taggedClip.Volume > 0f
                                ? taggedClip.Volume
                                : 1f;

                        guiSounds.PlaySound(
                            clip,
                            single: true,
                            commonUiSound: true,
                            volume: volume);

                        Plugin.Log?.LogInfo(
                            $"Blackjack stock-voice loss taunt: {phrase} (side={side ?? "unknown"}, voice={voiceName}, clip={clip.name})");

                        return true;
                    }

                    Plugin.Log?.LogWarning(
                        $"Blackjack stock voice '{voiceName}' loaded but contained none of the selected taunt banks.");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning(
                        $"Blackjack stock voice '{voiceName}' load failed: {ex.Message}");
                }
            }

            return false;
        }

        private static bool TryPlayPhraseSoundsFallback(
            InventoryController controller,
            EPhraseTrigger[] shuffled)
        {
            try
            {
                PhraseSounds[] phraseSounds =
                    Resources.FindObjectsOfTypeAll<
                        PhraseSounds>();

                if (phraseSounds == null ||
                    phraseSounds.Length == 0)
                {
                    Plugin.Log?.LogWarning(
                        "Blackjack PhraseSounds fallback: no PhraseSounds asset is loaded.");

                    return false;
                }

                string side =
                    TryGetProfileSide(
                        controller);

                foreach (PhraseSounds sounds
                    in phraseSounds)
                {
                    if (sounds == null ||
                        sounds.Voices == null ||
                        sounds.Voices.Length == 0)
                    {
                        continue;
                    }

                    Voice[] candidates =
                        sounds.Voices
                            .Where(
                                voice =>
                                    voice != null &&
                                    IsVoiceForSide(
                                        voice.Name,
                                        side))
                            .ToArray();

                    // If side lookup fails, use any USEC/BEAR PMC voice rather
                    // than a Scav/boss voice.
                    if (candidates.Length == 0)
                    {
                        candidates =
                            sounds.Voices
                                .Where(
                                    voice =>
                                        voice != null &&
                                        !string.IsNullOrEmpty(
                                            voice.Name) &&
                                        (
                                            voice.Name.StartsWith(
                                                "Usec",
                                                StringComparison.OrdinalIgnoreCase) ||
                                            voice.Name.StartsWith(
                                                "Bear",
                                                StringComparison.OrdinalIgnoreCase)
                                        ))
                                .ToArray();
                    }

                    if (candidates.Length == 0)
                    {
                        continue;
                    }

                    // Keep one native voice for this taunt attempt.
                    Voice voice =
                        candidates[
                            Random.Next(
                                candidates.Length)];

                    foreach (EPhraseTrigger phrase
                        in shuffled)
                    {
                        TagBank bank =
                            voice.Banks?
                                .FirstOrDefault(
                                    x =>
                                        x != null &&
                                        x.Trigger ==
                                        phrase);

                        if (bank == null ||
                            bank.Clips == null ||
                            bank.Clips.Length == 0)
                        {
                            continue;
                        }

                        TaggedClip taggedClip =
                            bank.Clips[
                                Random.Next(
                                    bank.Clips.Length)];

                        AudioClip clip =
                            taggedClip?.Clip;

                        if (clip == null)
                        {
                            continue;
                        }

                        GUISounds guiSounds =
                            ResolveGuiSounds();

                        if (guiSounds == null)
                        {
                            return false;
                        }

                        float volume =
                            taggedClip.Volume > 0f
                                ? taggedClip.Volume
                                : 1f;

                        guiSounds.PlaySound(
                            clip,
                            single: true,
                            commonUiSound: true,
                            volume: volume);

                        Plugin.Log?.LogInfo(
                            $"Blackjack PhraseSounds loss taunt: {phrase} (side={side ?? "unknown"}, voice={voice.Name}, clip={clip.name})");

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Blackjack PhraseSounds fallback failed: {ex.Message}");
            }

            return false;
        }

        private static string TryGetProfileSide(
            InventoryController controller)
        {
            if (controller?.Profile == null)
            {
                return null;
            }

            try
            {
                object profile =
                    controller.Profile;

                object info =
                    ReadMember(
                        profile,
                        "Info");

                foreach (object owner in new[]
                {
                    info,
                    profile
                })
                {
                    if (owner == null)
                    {
                        continue;
                    }

                    foreach (string memberName in new[]
                    {
                        "Side",
                        "PlayerSide",
                        "Faction"
                    })
                    {
                        object value =
                            ReadMember(
                                owner,
                                memberName);

                        string text =
                            value?.ToString();

                        if (!string.IsNullOrWhiteSpace(
                                text))
                        {
                            Plugin.Log?.LogInfo(
                                $"Resolved Blackjack profile side from {owner.GetType().Name}.{memberName}: {text}");

                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Blackjack profile-side lookup failed: {ex.Message}");
            }

            return null;
        }

        private static bool IsVoiceForSide(
            string voiceName,
            string side)
        {
            if (string.IsNullOrEmpty(
                    voiceName))
            {
                return false;
            }

            if (string.Equals(
                    side,
                    "Usec",
                    StringComparison.OrdinalIgnoreCase))
            {
                return voiceName.StartsWith(
                    "Usec",
                    StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(
                    side,
                    "Bear",
                    StringComparison.OrdinalIgnoreCase))
            {
                return voiceName.StartsWith(
                    "Bear",
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static string TryGetProfileVoice(
            InventoryController controller)
        {
            if (controller == null)
            {
                return null;
            }

            try
            {
                object profile =
                    controller.Profile;

                if (profile == null)
                {
                    return null;
                }

                // Current EFT profiles commonly expose voice under Info.Voice,
                // but reflection keeps this resilient if BSG changes the
                // concrete profile class/member visibility.
                object info =
                    ReadMember(
                        profile,
                        "Info");

                foreach (object owner in new[]
                {
                    info,
                    profile,
                    ReadMember(profile, "Customization")
                })
                {
                    if (owner == null)
                    {
                        continue;
                    }

                    foreach (string name in new[]
                    {
                        "Voice",
                        "VoiceId",
                        "VoiceID",
                        "VoiceName",
                        "PlayerVoice"
                    })
                    {
                        object value =
                            ReadMember(
                                owner,
                                name);

                        string text =
                            value?.ToString();

                        if (!string.IsNullOrWhiteSpace(
                                text))
                        {
                            Plugin.Log?.LogInfo(
                                $"Resolved Blackjack profile voice from {owner.GetType().Name}.{name}: {text}");

                            return text;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Blackjack profile voice lookup failed: {ex.Message}");
            }

            return null;
        }

        private static GUISounds ResolveGuiSounds()
        {
            try
            {
                GUISounds[] sounds =
                    Resources.FindObjectsOfTypeAll<
                        GUISounds>();

                if (sounds != null)
                {
                    foreach (GUISounds sound
                        in sounds)
                    {
                        if (sound != null)
                        {
                            return sound;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Blackjack GUISounds lookup failed: {ex.Message}");
            }

            return null;
        }

        private static EFT.Player FindLocalPlayer(
            string profileId)
        {
            try
            {
                EFT.Player[] players =
                    Resources.FindObjectsOfTypeAll<
                        EFT.Player>();

                if (players == null ||
                    players.Length == 0)
                {
                    return null;
                }

                foreach (EFT.Player player
                    in players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    string candidateId =
                        TryGetProfileId(
                            player);

                    if (!string.IsNullOrEmpty(
                            candidateId) &&
                        candidateId ==
                        profileId)
                    {
                        return player;
                    }
                }

                if (players.Length == 1)
                {
                    return players[0];
                }

                foreach (EFT.Player player
                    in players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    object value =
                        ReadMember(
                            player,
                            "IsYourPlayer");

                    if (value is bool &&
                        (bool)value)
                    {
                        return player;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning(
                    $"Could not locate EFT player for Blackjack voice: {ex.Message}");
            }

            return null;
        }

        private static string TryGetProfileId(
            object player)
        {
            object direct =
                ReadMember(
                    player,
                    "ProfileId")
                ?? ReadMember(
                    player,
                    "ProfileID");

            if (direct != null)
            {
                return direct.ToString();
            }

            object profile =
                ReadMember(
                    player,
                    "Profile");

            if (profile == null)
            {
                return null;
            }

            object id =
                ReadMember(
                    profile,
                    "Id")
                ?? ReadMember(
                    profile,
                    "ProfileId")
                ?? ReadMember(
                    profile,
                    "ProfileID");

            return id?.ToString();
        }

        private static object ReadMember(
            object target,
            string name)
        {
            if (target == null)
            {
                return null;
            }

            Type type =
                target.GetType();

            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null)
            {
                try
                {
                    return property.GetValue(
                        target);
                }
                catch
                {
                }
            }

            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (field != null)
            {
                try
                {
                    return field.GetValue(
                        target);
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
