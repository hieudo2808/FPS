using System;
using UnityEngine;

namespace FPS
{
    [Serializable]
    public sealed class CampaignRunSummary
    {
        public CampaignPhase result;
        public float durationSeconds;
        public int survivors;
        public int rosterSize;
        public int teamDowns;
        public int zombieKills;
        public int filesRecorded;
        public int optionalFilesFound;
        public int optionalFilesTotal;
        public bool factoryCaseSecured;
        public bool labCaseSecured;
        public CampaignMemberResult[] members = Array.Empty<CampaignMemberResult>();
    }

    [Serializable]
    public sealed class CampaignMemberResult
    {
        public ulong playerId;
        public PlayerCharacterId character;
        public PlayerLifeState lifeState;
        public bool connected;
    }

    [Serializable]
    public sealed class CampaignFileDefinition
    {
        public CampaignFileId id;
        public CampaignChapter chapter;
        public bool required;
        public string title;
        [TextArea(2, 6)] public string summary;
        [TextArea(4, 30)] public string body;
        [TextArea(4, 30)] public string bodyVietnamese;
        public AudioClip audio;
        public Sprite icon;
        public Sprite preview;
        public CampaignObjectiveId autoDiscoverAfter = CampaignObjectiveId.None;
        public int order;

    }

    [Serializable]
    public sealed class CampaignKeyItemDefinition
    {
        public CampaignKeyItemId id;
        public CampaignChapter chapter;
        public string title;
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public CampaignObjectiveId acquiredAfter = CampaignObjectiveId.None;
        public CampaignObjectiveId consumedAfter = CampaignObjectiveId.None;
        public bool persistAfterUse;
        public int order;

        public bool IsOwned(CampaignState state) => state != null && state.Has(acquiredAfter)
            && (persistAfterUse || consumedAfter == CampaignObjectiveId.None || !state.Has(consumedAfter));
    }

    [Serializable]
    public sealed class CampaignDialogueDefinition
    {
        public CampaignDialogueId id;
        public string speaker;
        [TextArea(2, 6)] public string english;
        [TextArea(2, 6)] public string vietnamese;
        public byte priority;
        public float cooldown;
        public bool oncePerRun;
        public string trigger;
        public AudioClip audio;
    }

    [Serializable]
    public sealed class CampaignObjectiveDefinition
    {
        public CampaignObjectiveId id;
        public string title;
        [TextArea(3, 20)] public string instructions;
        public string hint;
        public string[] choices = Array.Empty<string>();
        public string[] secondaryChoices = Array.Empty<string>();
        public CampaignFileId file;
    }

    [CreateAssetMenu(menuName = "FPS/Campaign/Content Catalog")]
    public sealed class CampaignContentCatalog : ScriptableObject
    {
        public CampaignFileDefinition[] files = Array.Empty<CampaignFileDefinition>();
        public CampaignKeyItemDefinition[] keyItems = Array.Empty<CampaignKeyItemDefinition>();
        public CampaignDialogueDefinition[] dialogue = Array.Empty<CampaignDialogueDefinition>();
        public CampaignObjectiveDefinition[] objectives = Array.Empty<CampaignObjectiveDefinition>();

        public CampaignObjectiveDefinition FindObjective(CampaignObjectiveId id)
        {
            if (objectives != null)
                foreach (var item in objectives) if (item != null && item.id == id) return item;
            return null;
        }

        public CampaignFileDefinition FindFile(CampaignFileId id)
        {
            if (id == CampaignFileId.None || files == null) return null;
            for (int i = 0; i < files.Length; i++)
                if (files[i] != null && files[i].id == id) return files[i];
            return null;
        }

        public CampaignKeyItemDefinition FindKeyItem(CampaignKeyItemId id)
        {
            if (id == CampaignKeyItemId.None || keyItems == null) return null;
            for (int i = 0; i < keyItems.Length; i++)
                if (keyItems[i] != null && keyItems[i].id == id) return keyItems[i];
            return null;
        }

        public CampaignDialogueDefinition FindDialogue(CampaignDialogueId id)
        {
            if (id == CampaignDialogueId.None || dialogue == null) return null;
            for (int i = 0; i < dialogue.Length; i++)
                if (dialogue[i] != null && dialogue[i].id == id) return dialogue[i];
            return null;
        }
    }
}
