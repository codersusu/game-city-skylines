using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seabright
{
    // Append kinds: their numeric values are the on-disk save identifiers.
    public enum TileKind { Empty, Road, Residential, Commercial, Industrial, Park, Power, Water, Clinic, HighResidential, Office, Stadium }

    [Serializable]
    public class CityTile
    {
        public int X, Z, Level, Residents, Jobs;
        public TileKind Kind;
        public float Growth, LandValue, Pollution;
        public bool Connected, Powered, Watered;
        public int AnchorX = -1, AnchorZ = -1;
    }

    /// <summary>A deterministic, tile-based city economy. Every public edit is validated before it commits.</summary>
    public class CitySimulation
    {
        public const int Size = 48;
        public const float CellSize = 10f;
        public static readonly Vector2Int Gateway = new Vector2Int(3, 24);
        public CityTile[] Tiles;
        public float Money, Income, Expenses, Happiness, TrafficFlow, PowerUse, PowerCapacity,
            WaterUse, WaterCapacity, ResidentialDemand, CommercialDemand, IndustrialDemand;
        public int Population, Jobs, Day, Revision;
        public int PeakPopulation { get; private set; }
        public float TaxRate = 11f;

        public string MilestoneName => PeakPopulation >= 600 ? "Coastal city" : PeakPopulation >= 350 ? "Rising skyline" : PeakPopulation >= 150 ? "Growing town" : "New settlement";
        public int NextMilestonePopulation => PeakPopulation < 150 ? 150 : PeakPopulation < 350 ? 350 : PeakPopulation < 600 ? 600 : 0;

        const float PowerRange = 26f, WaterRange = 24f;
        float elapsedDays, pendingDays;
        readonly float[] roadLoads = new float[Size * Size];
        readonly int[] visit = new int[Size * Size];
        readonly int[] parent = new int[Size * Size];
        readonly int[] queue = new int[Size * Size];
        int searchId;

        public CitySimulation() { CreateGrid(); Day = 1; Money = 100000f; }

        void CreateGrid()
        {
            Tiles = new CityTile[Size * Size];
            for (int z = 0; z < Size; z++)
                for (int x = 0; x < Size; x++)
                    Tiles[Index(x, z)] = new CityTile { X = x, Z = z };
        }

        static int Index(int x, int z) { return z * Size + x; }
        static bool InBounds(int x, int z) { return x >= 0 && x < Size && z >= 0 && z < Size; }
        public static bool IsZone(TileKind kind) { return kind >= TileKind.Residential && kind <= TileKind.Industrial || kind == TileKind.HighResidential || kind == TileKind.Office; }
        public static bool IsResidential(TileKind kind) { return kind == TileKind.Residential || kind == TileKind.HighResidential; }
        static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        static int Hash(int x, int z) { return ((x * 73856093) ^ (z * 19349663)) & 0x7fffffff; }
        static int Capacity(int level) { return level <= 0 ? 0 : level == 1 ? 16 : level == 2 ? 28 : level == 3 ? 44 : 64; }
        public static int ResidentCapacity(CityTile t) { return !IsResidential(t.Kind) ? 0 : t.Kind == TileKind.HighResidential ? (t.Level == 0 ? 0 : 32 + t.Level * 32) : Capacity(t.Level); }
        static int JobCapacity(CityTile t)
        {
            if (t.Level == 0 || !IsFootprintAnchor(t)) return 0;
            return t.Kind == TileKind.Commercial ? 4 + t.Level * 4 : t.Kind == TileKind.Industrial ? 8 + t.Level * 8 : t.Kind == TileKind.Office ? 32 + t.Level * 16 : t.Kind == TileKind.Stadium ? 64 : 0;
        }

        public int UnlockRequirement(TileKind kind) { return kind == TileKind.Office ? 150 : kind == TileKind.HighResidential ? 350 : kind == TileKind.Stadium ? 600 : 0; }
        public bool IsUnlocked(TileKind kind) { return Enum.IsDefined(typeof(TileKind), kind) && PeakPopulation >= UnlockRequirement(kind); }
        public static bool IsFootprintAnchor(CityTile t) { return t != null && (t.Kind != TileKind.Stadium || t.AnchorX == t.X && t.AnchorZ == t.Z); }
        public CityTile GetAnchor(CityTile t) { return t != null && t.Kind == TileKind.Stadium ? Get(t.AnchorX, t.AnchorZ) : t; }

        public void SeedStarterTown()
        {
            CreateGrid();
            TaxRate = 11f; Day = 1; elapsedDays = pendingDays = 0f; Money = 30000f; PeakPopulation = 24;
            for (int x = Gateway.x; x <= 15; x++) Get(x, 24).Kind = TileKind.Road;
            SetSeed(4, 23, TileKind.Power); SetSeed(5, 25, TileKind.Water);
            SetSeed(12, 23, TileKind.Residential); Get(12, 23).Residents = 12;
            SetSeed(13, 23, TileKind.Residential); Get(13, 23).Residents = 12;
            SetSeed(11, 25, TileKind.Commercial); SetSeed(7, 26, TileKind.Industrial);
            Recalculate(); Revision++;
        }

        public CityTile Get(int x, int z) { return InBounds(x, z) ? Tiles[Index(x, z)] : null; }
        public static bool IsLand(int x, int z)
        {
            return x >= 3 && x <= 43 && z >= 5 && z <= 44 && x < 38f + 2f * Mathf.Sin(z * .19f);
        }
        public static Vector3 World(int x, int z) { return new Vector3((x - 23.5f) * CellSize, 0f, (z - 23.5f) * CellSize); }

        public void SeedCity()
        {
            CreateGrid();
            TaxRate = 11f; Day = 1; elapsedDays = 0f; pendingDays = 0f; PeakPopulation = 2124;
            for (int z = 14; z <= 34; z++)
                for (int x = 13; x <= 33; x++)
                {
                    var t = Get(x, z);
                    if ((x - 13) % 5 == 0 || (z - 14) % 5 == 0) { t.Kind = TileKind.Road; continue; }
                    int blockX = (x - 13) / 5, blockZ = (z - 14) / 5;
                    int localX = (x - 13) % 5, localZ = (z - 14) % 5;
                    int h = Hash(x, z);
                    if (localX == 2 && localZ == 2) { t.Kind = TileKind.Park; t.Level = 1; continue; }
                    if (localX == 3 && localZ == 2 && (blockX + blockZ) % 2 == 0) continue;
                    if (x < 23)
                    {
                        t.Kind = TileKind.Residential;
                        t.Level = h % 5 < 2 ? 2 : 1;
                    }
                    else if (z >= 30)
                    {
                        t.Kind = TileKind.Industrial;
                        t.Level = h % 3 == 0 ? 2 : 1;
                    }
                    else if (z >= 25 || (x >= 29 && z >= 20 && h % 3 == 0))
                    {
                        t.Kind = TileKind.Residential;
                        t.Level = x >= 29 ? 3 + h % 2 : 2 + h % 2;
                    }
                    else
                    {
                        t.Kind = TileKind.Commercial;
                        t.Level = x >= 29 ? 3 + h % 2 : 2 + h % 2;
                    }
                }
            for (int x = Gateway.x; x <= 13; x++) Get(x, Gateway.y).Kind = TileKind.Road;
            SetSeed(14, 33, TileKind.Power);
            SetSeed(32, 33, TileKind.Power);
            SetSeed(14, 15, TileKind.Water);
            SetSeed(32, 25, TileKind.Water);
            SetSeed(19, 20, TileKind.Clinic);
            SetSeed(27, 25, TileKind.Clinic);
            // A park promenade provides a green transition from the waterfront skyline.
            SetSeed(32, 15, TileKind.Park);
            SetSeed(32, 16, TileKind.Park);
            SetSeed(32, 17, TileKind.Park);
            SetSeed(32, 18, TileKind.Park);

            int capacity = 0;
            foreach (var t in Tiles) if (t.Kind == TileKind.Residential) capacity += Capacity(t.Level);
            int target = Math.Min(2124, capacity), allocated = 0;
            foreach (var t in Tiles)
                if (t.Kind == TileKind.Residential)
                {
                    t.Residents = Mathf.FloorToInt(Capacity(t.Level) * (float)target / capacity);
                    allocated += t.Residents;
                }
            foreach (var t in Tiles)
                if (allocated < target && t.Kind == TileKind.Residential && t.Residents < Capacity(t.Level)) { t.Residents++; allocated++; }
            Money = 100000f;
            Recalculate();
            Revision++;
        }

        void SetSeed(int x, int z, TileKind kind)
        {
            var t = Get(x, z); t.Kind = kind; t.Level = 1; t.Residents = 0; t.Jobs = 0; t.Growth = 0;
        }

        public float Cost(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Road: return 80f;
                case TileKind.Residential: return 45f;
                case TileKind.Commercial: return 60f;
                case TileKind.Industrial: return 55f;
                case TileKind.Park: return 700f;
                case TileKind.Power: return 6500f;
                case TileKind.Water: return 5000f;
                case TileKind.Clinic: return 4200f;
                case TileKind.HighResidential: return 1400f;
                case TileKind.Office: return 2200f;
                case TileKind.Stadium: return 14000f;
                default: return 0f;
            }
        }

        public bool Build(int x, int z, TileKind kind, out string reason)
        {
            if (kind == TileKind.Empty) return Bulldoze(x, z, out reason);
            if (!Enum.IsDefined(typeof(TileKind), kind)) { reason = "Unknown construction type."; return false; }
            if (!IsUnlocked(kind)) { reason = "Reach " + UnlockRequirement(kind) + " residents to unlock " + kind + "."; return false; }
            if (!IsLand(x, z)) { reason = "Build on dry land inside the city boundary."; return false; }
            var t = Get(x, z);
            if (t.Kind == kind) { reason = "This tile is already " + kind.ToString().ToLowerInvariant() + "."; return true; }
            if (kind == TileKind.Stadium) return BuildStadium(x, z, out reason);
            if (t.Kind != TileKind.Empty && !(IsZone(t.Kind) && IsZone(kind)))
            { reason = "Bulldoze the existing structure first."; return false; }
            float cost = Cost(kind);
            if (!Finite(Money) || Money < cost) { reason = "Insufficient treasury funds."; return false; }
            Money -= cost;
            ResetTile(t, kind);
            Recalculate(); Revision++;
            reason = IsZone(kind) ? "Zone designated. Growth needs road access, utilities and demand." : kind + " constructed.";
            return true;
        }

        bool BuildStadium(int x, int z, out string reason)
        {
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var part = Get(x + dx, z + dz);
                    if (!IsLand(x + dx, z + dz) || part == null || part.Kind != TileKind.Empty)
                    { reason = "The stadium needs a clear 3 × 3 area, with a road beside it."; return false; }
                }
            if (RoadAccess(x, z).x < 0) { reason = "Place the stadium beside a connected road."; return false; }
            if (!Finite(Money) || Money < Cost(TileKind.Stadium)) { reason = "Insufficient treasury funds for the stadium."; return false; }
            Money -= Cost(TileKind.Stadium);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var part = Get(x + dx, z + dz); ResetTile(part, TileKind.Stadium);
                    part.AnchorX = x; part.AnchorZ = z;
                }
            Recalculate(); Revision++;
            reason = "Seabright Stadium opened. Connect power and water to welcome visitors.";
            return true;
        }

        public bool BuildRoad(int x0, int z0, int x1, int z1, out string reason)
        {
            if (!InBounds(x0, z0) || !InBounds(x1, z1)) { reason = "Road endpoint is outside the city boundary."; return false; }
            var points = RoadLine(x0, z0, x1, z1, true);
            float cost;
            if (!ValidateRoad(points, out cost, out reason))
            {
                if (x0 == x1 || z0 == z1) return false;
                var alternate = RoadLine(x0, z0, x1, z1, false);
                string alternateReason;
                float alternateCost;
                if (!ValidateRoad(alternate, out alternateCost, out alternateReason)) return false;
                points = alternate; cost = alternateCost;
            }
            if (cost == 0) { reason = "Road is already built."; return true; }
            Money -= cost;
            foreach (var point in points)
            {
                var t = Get(point.x, point.y);
                if (t.Kind != TileKind.Road) ResetTile(t, TileKind.Road);
            }
            Recalculate(); Revision++;
            reason = "Road connected: $" + cost.ToString("N0") + ".";
            return true;
        }

        static List<Vector2Int> RoadLine(int x0, int z0, int x1, int z1, bool horizontalFirst)
        {
            var path = new List<Vector2Int> { new Vector2Int(x0, z0) };
            int x = x0, z = z0;
            if (horizontalFirst)
            {
                while (x != x1) { x += x1 > x ? 1 : -1; path.Add(new Vector2Int(x, z)); }
                while (z != z1) { z += z1 > z ? 1 : -1; path.Add(new Vector2Int(x, z)); }
            }
            else
            {
                while (z != z1) { z += z1 > z ? 1 : -1; path.Add(new Vector2Int(x, z)); }
                while (x != x1) { x += x1 > x ? 1 : -1; path.Add(new Vector2Int(x, z)); }
            }
            return path;
        }

        bool ValidateRoad(List<Vector2Int> points, out float cost, out string reason)
        {
            cost = 0f; reason = "";
            foreach (var p in points)
            {
                if (!IsLand(p.x, p.y)) { reason = "Road cannot cross the shoreline."; return false; }
                var t = Get(p.x, p.y);
                if (t.Kind != TileKind.Empty && t.Kind != TileKind.Road) { reason = "Road blocked by a structure. Bulldoze it first."; return false; }
                if (t.Kind == TileKind.Empty) cost += Cost(TileKind.Road);
            }
            if (!Finite(Money) || Money < cost) { reason = "Insufficient treasury funds for the complete road."; return false; }
            return true;
        }

        public bool Bulldoze(int x, int z, out string reason)
        {
            var t = Get(x, z);
            if (t == null) { reason = "Outside the city boundary."; return false; }
            if (t.Kind == TileKind.Empty) { reason = "This tile is already clear."; return true; }
            var previous = t.Kind;
            if (previous == TileKind.Stadium)
            {
                var anchor = GetAnchor(t);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++) ResetTile(Get(anchor.X + dx, anchor.Z + dz), TileKind.Empty);
            }
            else ResetTile(t, TileKind.Empty);
            Recalculate(); Revision++;
            reason = previous + " removed.";
            return true;
        }

        static void ResetTile(CityTile t, TileKind kind)
        {
            t.Kind = kind; t.Level = IsZone(kind) || kind == TileKind.Empty || kind == TileKind.Road ? 0 : 1;
            t.Residents = 0; t.Jobs = 0; t.Growth = 0f;
            t.AnchorX = t.AnchorZ = -1;
        }

        public void Tick(float days)
        {
            if (!Finite(days) || days <= 0f) return;
            // Fixed steps prevent frame-rate-dependent growth and unnecessary whole-city work.
            pendingDays += Mathf.Min(days, 3650f);
            if (pendingDays + .00001f < .1f) return;
            int steps = Mathf.FloorToInt((pendingDays + .00001f) / .1f);
            pendingDays = Mathf.Max(0f, pendingDays - steps * .1f);
            Recalculate();
            for (int iteration = 0; iteration < steps; iteration++)
            {
                const float step = .1f;
                Money = Mathf.Clamp(Money + (Income - Expenses) * step, -1000000000f, 1000000000f);
                elapsedDays += step;
                if (elapsedDays >= 1f) { int advance = Mathf.FloorToInt(elapsedDays); Day += advance; elapsedDays -= advance; }
                foreach (var t in Tiles)
                {
                    if (!IsZone(t.Kind)) continue;
                    float demand = IsResidential(t.Kind) ? ResidentialDemand : t.Kind == TileKind.Industrial ? IndustrialDemand : CommercialDemand;
                    bool serviced = t.Connected && t.Powered && t.Watered;
                    if (!serviced || (IsResidential(t.Kind) && (Happiness < 30f || TaxRate > 20f)))
                    {
                        if (t.Residents > 0)
                        {
                            t.Growth += step * .4f;
                            while (t.Growth >= 1f && t.Residents > 0) { t.Residents--; t.Growth -= 1f; }
                        }
                        else t.Growth = Mathf.Max(0f, t.Growth - step * .03f);
                        continue;
                    }
                    if (t.Level == 0)
                    {
                        if (demand <= 8f || (IsResidential(t.Kind) && t.Pollution > 65f)) continue;
                        t.Growth += step * (.35f + demand * .004f);
                        if (t.Growth >= 1f) { t.Level = 1; t.Growth = 0f; t.Residents = IsResidential(t.Kind) ? (t.Kind == TileKind.HighResidential ? 12 : 4) : 0; }
                    }
                    else if (IsResidential(t.Kind) && t.Residents < ResidentCapacity(t))
                    {
                        if (demand <= 8f) continue;
                        t.Growth += step * (1.25f + demand * .03f) * (t.Kind == TileKind.HighResidential ? 2.5f : 1f) * Mathf.Lerp(.4f, 1.2f, t.LandValue / 100f);
                        while (t.Growth >= 1f && t.Residents < ResidentCapacity(t)) { t.Residents++; t.Growth -= 1f; }
                    }
                    else if (t.Level < (t.Kind == TileKind.Residential || t.Kind == TileKind.Commercial ? 2 : 4) && demand > 48f && t.LandValue > (t.Kind == TileKind.Industrial ? 15f : 46f) && TaxRate <= 15f)
                    {
                        t.Growth += step * .12f * demand / 100f;
                        if (t.Growth >= 1f) { t.Level++; t.Growth = 0f; }
                    }
                    t.Growth = Mathf.Clamp01(t.Growth);
                }
                Recalculate();
            }
            Revision++;
        }

        public void Recalculate()
        {
            TaxRate = Finite(TaxRate) ? Mathf.Clamp(TaxRate, 1f, 25f) : 11f;
            foreach (var t in Tiles) { t.Connected = false; t.Powered = false; t.Watered = false; t.Pollution = 0f; t.Jobs = 0; }
            MarkConnectedRoads();
            var plants = new List<CityTile>(); var pumps = new List<CityTile>();
            var parks = new List<CityTile>(); var clinics = new List<CityTile>(); var stadiums = new List<CityTile>(); var polluters = new List<CityTile>();
            Population = 0; Jobs = 0; PowerCapacity = 0; WaterCapacity = 0;
            int roadCount = 0;
            foreach (var t in Tiles)
            {
                if (t.Kind != TileKind.Road) t.Connected = RoadAccess(t.X, t.Z).x >= 0;
                if (t.Kind == TileKind.Road) roadCount++;
                if (IsResidential(t.Kind)) Population += t.Residents;
                if (t.Kind == TileKind.Power && t.Connected) { plants.Add(t); PowerCapacity += 3000f; polluters.Add(t); }
                if (t.Kind == TileKind.Water && t.Connected) { pumps.Add(t); WaterCapacity += 2400f; }
                if (t.Kind == TileKind.Park && t.Connected) parks.Add(t);
                if (t.Kind == TileKind.Clinic && t.Connected) clinics.Add(t);
                if (t.Kind == TileKind.Stadium && t.Connected && IsFootprintAnchor(t)) stadiums.Add(t);
                if (t.Kind == TileKind.Industrial && t.Level > 0 && t.Connected) polluters.Add(t);
            }

            PowerUse = 0f; WaterUse = 0f;
            var powerNeeds = new float[Tiles.Length]; var waterNeeds = new float[Tiles.Length];
            foreach (var t in Tiles)
            {
                int id = Index(t.X, t.Z);
                powerNeeds[id] = PowerNeed(t); waterNeeds[id] = WaterNeed(t);
                if (t.Kind != TileKind.Empty && t.Kind != TileKind.Road)
                { PowerUse += powerNeeds[id]; WaterUse += waterNeeds[id]; }
                t.Powered = t.Connected && InServiceRange(t, plants, PowerRange);
                t.Watered = t.Connected && InServiceRange(t, pumps, WaterRange);
                if (t.Kind == TileKind.Power && t.Connected) t.Powered = true;
                if (t.Kind == TileKind.Water && t.Connected) t.Watered = true;
            }
            // Deterministic load shedding preserves service to civic buildings before private demand.
            AllocateUtility(true, powerNeeds, PowerCapacity);
            AllocateUtility(false, waterNeeds, WaterCapacity);
            int commercialJobs = 0, industrialJobs = 0;
            float happinessSum = 0f, occupiedWeight = 0f;
            foreach (var t in Tiles)
            {
                if (!IsLand(t.X, t.Z)) { t.LandValue = 0f; continue; }
                foreach (var source in polluters)
                {
                    float distance = Mathf.Abs(t.X - source.X) + Mathf.Abs(t.Z - source.Z);
                    float radius = source.Kind == TileKind.Power ? 6f : 5f;
                    if (distance < radius) t.Pollution += (source.Kind == TileKind.Power ? 24f : 13f + source.Level * 5f) * (1f - distance / radius);
                }
                t.Pollution = Mathf.Clamp(t.Pollution, 0f, 100f);
                float green = ServiceInfluence(t, parks, 7f, 11f, false);
                float health = ServiceInfluence(t, clinics, 13f, 18f, true);
                float recreation = ServiceInfluence(t, stadiums, 15f, 12f, true);
                float coastal = Mathf.Clamp01((t.X - 23f) / 15f) * 16f;
                t.LandValue = Mathf.Clamp(39f + coastal + Mathf.Min(27f, green) + Mathf.Min(12f, health) + Mathf.Min(8f, recreation) + (t.Powered && t.Watered ? 7f : -8f) - t.Pollution * .62f, 0f, 100f);
                if (t.Connected && t.Powered && t.Watered)
                {
                    t.Jobs = JobCapacity(t);
                    Jobs += t.Jobs;
                    if (t.Kind == TileKind.Commercial || t.Kind == TileKind.Office || t.Kind == TileKind.Stadium) commercialJobs += t.Jobs;
                    if (t.Kind == TileKind.Industrial) industrialJobs += t.Jobs;
                }
                if (IsResidential(t.Kind) && t.Residents > 0)
                {
                    float local = 69f + Mathf.Min(11f, green * .5f) + Mathf.Min(9f, health * .5f) + Mathf.Min(6f, recreation * .5f)
                        - t.Pollution * .26f - Mathf.Max(0f, TaxRate - 11f) * 3f
                        - (t.Connected ? 0f : 24f) - (t.Powered ? 0f : 29f) - (t.Watered ? 0f : 29f);
                    happinessSum += local * t.Residents; occupiedWeight += t.Residents;
                }
            }
            CalculateTraffic();
            float workforce = Population * .46f;
            float employment = workforce > 0 ? Mathf.Clamp01(Jobs / workforce) : 1f;
            Happiness = Mathf.Clamp((occupiedWeight > 0 ? happinessSum / occupiedWeight : 70f) + employment * 7f - (100f - TrafficFlow) * .08f, 0f, 100f);
            float workGap = (Jobs - workforce) / Mathf.Max(200f, workforce);
            ResidentialDemand = Mathf.Clamp(45f + workGap * 50f + (Happiness - 65f) * .8f - (TaxRate - 11f) * 4f, 0f, 100f);
            CommercialDemand = Mathf.Clamp(43f + (workforce * .62f - commercialJobs) / Mathf.Max(100f, workforce * .62f) * 64f + (Happiness - 70f) * .2f - (TaxRate - 11f) * 3f, 0f, 100f);
            IndustrialDemand = Mathf.Clamp(44f + (workforce * .52f - industrialJobs) / Mathf.Max(100f, workforce * .52f) * 60f - (TaxRate - 11f) * 3f, 0f, 100f);
            float staffing = Jobs > 0 ? Mathf.Clamp01(workforce / Jobs) : 0f;
            float taxScale = TaxRate / 11f;
            Income = (Population * .46f + (commercialJobs * .74f + industrialJobs * .58f) * staffing) * taxScale * Mathf.Lerp(.74f, 1f, Happiness / 100f);
            Expenses = roadCount * .36f;
            foreach (var t in Tiles)
            {
                switch (t.Kind)
                {
                    case TileKind.Power: Expenses += 32f; break;
                    case TileKind.Water: Expenses += 24f; break;
                    case TileKind.Clinic: Expenses += 74f; break;
                    case TileKind.Park: Expenses += 7f; break;
                    case TileKind.Stadium: if (IsFootprintAnchor(t)) Expenses += 96f; break;
                }
            }
            // Grants are awarded once; the saved population record prevents repeated unlock rewards.
            if (Population > PeakPopulation)
            {
                float grant = 0f;
                if (PeakPopulation < 150 && Population >= 150) grant += 6000f;
                if (PeakPopulation < 350 && Population >= 350) grant += 10000f;
                if (PeakPopulation < 600 && Population >= 600) grant += 15000f;
                Money = Mathf.Min(1000000000f, Money + grant);
                PeakPopulation = Population;
            }
        }

        static float PowerNeed(CityTile t)
        {
            if (!IsFootprintAnchor(t)) return 0f;
            if (IsResidential(t.Kind)) return Mathf.Max(.5f, t.Residents * .6f + t.Level);
            if (t.Kind == TileKind.Commercial) return Mathf.Max(.5f, JobCapacity(t) * .7f + t.Level * 2f);
            if (t.Kind == TileKind.Office) return Mathf.Max(.5f, JobCapacity(t) * .8f + t.Level * 4f);
            if (t.Kind == TileKind.Stadium) return 120f;
            if (t.Kind == TileKind.Industrial) return Mathf.Max(.5f, JobCapacity(t) * 1.1f + t.Level * 5f);
            if (t.Kind == TileKind.Clinic) return 25f;
            if (t.Kind == TileKind.Water) return 20f;
            return t.Kind == TileKind.Park ? 2f : 0f;
        }
        static float WaterNeed(CityTile t)
        {
            if (!IsFootprintAnchor(t)) return 0f;
            if (IsResidential(t.Kind)) return Mathf.Max(.5f, t.Residents * .45f);
            if (t.Kind == TileKind.Commercial) return Mathf.Max(.5f, JobCapacity(t) * .38f);
            if (t.Kind == TileKind.Office) return Mathf.Max(.5f, JobCapacity(t) * .3f);
            if (t.Kind == TileKind.Stadium) return 90f;
            if (t.Kind == TileKind.Industrial) return Mathf.Max(.5f, JobCapacity(t) * .75f);
            if (t.Kind == TileKind.Power) return 30f;
            if (t.Kind == TileKind.Clinic) return 18f;
            return t.Kind == TileKind.Park ? 3f : 0f;
        }

        void AllocateUtility(bool power, float[] needs, float capacity)
        {
            for (int pass = 0; pass < 2; pass++)
                foreach (var t in Tiles)
                {
                    if (t.Kind == TileKind.Empty || t.Kind == TileKind.Road) continue;
                    bool civic = !IsZone(t.Kind);
                    if (civic != (pass == 0) || !(power ? t.Powered : t.Watered)) continue;
                    float need = needs[Index(t.X, t.Z)];
                    if (capacity + .001f >= need) capacity -= need;
                    else if (power) t.Powered = false;
                    else t.Watered = false;
                }
        }

        static bool InServiceRange(CityTile t, List<CityTile> sources, float radius)
        {
            foreach (var s in sources) if (Mathf.Abs(t.X - s.X) + Mathf.Abs(t.Z - s.Z) <= radius) return true;
            return false;
        }
        static float ServiceInfluence(CityTile t, List<CityTile> sources, float radius, float strength, bool requiresUtilities)
        {
            float influence = 0f;
            foreach (var source in sources)
            {
                if (requiresUtilities && (!source.Powered || !source.Watered)) continue;
                float distance = Mathf.Abs(t.X - source.X) + Mathf.Abs(t.Z - source.Z);
                influence += Mathf.Max(0f, 1f - distance / radius) * strength;
            }
            return influence;
        }

        void MarkConnectedRoads()
        {
            var start = Get(Gateway.x, Gateway.y);
            if (start.Kind != TileKind.Road) return;
            int head = 0, tail = 0;
            queue[tail++] = Index(start.X, start.Z); start.Connected = true;
            while (head < tail)
            {
                int id = queue[head++], x = id % Size, z = id / Size;
                ConnectNeighbor(x - 1, z, ref tail); ConnectNeighbor(x + 1, z, ref tail);
                ConnectNeighbor(x, z - 1, ref tail); ConnectNeighbor(x, z + 1, ref tail);
            }
        }
        void ConnectNeighbor(int x, int z, ref int tail)
        {
            var t = Get(x, z);
            if (t == null || t.Kind != TileKind.Road || t.Connected) return;
            t.Connected = true; queue[tail++] = Index(x, z);
        }

        /// <summary>Frontage extends two cells from a connected road, matching the four-cell city blocks.</summary>
        public Vector2Int RoadAccess(int x, int z)
        {
            var self = Get(x, z);
            if (self != null && self.Kind == TileKind.Road && self.Connected) return new Vector2Int(x, z);
            for (int d = 1; d <= 2; d++)
            {
                if (ConnectedRoad(x - d, z)) return new Vector2Int(x - d, z);
                if (ConnectedRoad(x + d, z)) return new Vector2Int(x + d, z);
                if (ConnectedRoad(x, z - d)) return new Vector2Int(x, z - d);
                if (ConnectedRoad(x, z + d)) return new Vector2Int(x, z + d);
            }
            return new Vector2Int(-1, -1);
        }
        bool ConnectedRoad(int x, int z) { var t = Get(x, z); return t != null && t.Kind == TileKind.Road && t.Connected; }

        public List<Vector2Int> FindRoadPath(Vector2Int from, Vector2Int to)
        {
            var result = new List<Vector2Int>();
            var a = Get(from.x, from.y); var b = Get(to.x, to.y);
            if (a == null || b == null || a.Kind != TileKind.Road || b.Kind != TileKind.Road) return result;
            if (++searchId == int.MaxValue) { Array.Clear(visit, 0, visit.Length); searchId = 1; }
            int start = Index(from.x, from.y), goal = Index(to.x, to.y), head = 0, tail = 0;
            queue[tail++] = start; visit[start] = searchId; parent[start] = -1;
            while (head < tail)
            {
                int id = queue[head++];
                if (id == goal)
                {
                    for (int n = goal; n >= 0; n = parent[n]) result.Add(new Vector2Int(n % Size, n / Size));
                    result.Reverse(); return result;
                }
                int x = id % Size, z = id / Size;
                SearchNeighbor(x - 1, z, id, ref tail); SearchNeighbor(x + 1, z, id, ref tail);
                SearchNeighbor(x, z - 1, id, ref tail); SearchNeighbor(x, z + 1, id, ref tail);
            }
            return result;
        }
        void SearchNeighbor(int x, int z, int previous, ref int tail)
        {
            var t = Get(x, z);
            if (t == null || t.Kind != TileKind.Road) return;
            int id = Index(x, z);
            if (visit[id] == searchId) return;
            visit[id] = searchId; parent[id] = previous; queue[tail++] = id;
        }

        void CalculateTraffic()
        {
            Array.Clear(roadLoads, 0, roadLoads.Length);
            var homes = new List<CityTile>(); var work = new List<CityTile>();
            foreach (var t in Tiles)
            {
                if (t.Connected && t.Residents > 0) homes.Add(t);
                if (t.Connected && t.Jobs > 0) work.Add(t);
            }
            if (homes.Count > 0 && work.Count > 0)
            {
                int samples = Math.Min(24, homes.Count);
                float scale = (float)homes.Count / samples;
                for (int i = 0; i < samples; i++)
                {
                    var home = homes[i * homes.Count / samples];
                    var destination = work[(i * 17 + 11) % work.Count];
                    var path = FindRoadPath(RoadAccess(home.X, home.Z), RoadAccess(destination.X, destination.Z));
                    float load = home.Residents * .46f * scale / 8f;
                    foreach (var p in path) roadLoads[Index(p.x, p.y)] += load;
                }
            }
            var loads = new List<float>(); float total = 0f;
            foreach (var t in Tiles)
                if (t.Kind == TileKind.Road && t.Connected) { float load = roadLoads[Index(t.X, t.Z)]; loads.Add(load); total += load; }
            loads.Sort();
            float average = loads.Count > 0 ? total / loads.Count : 0f;
            float busy = loads.Count > 0 ? loads[Mathf.Min(loads.Count - 1, Mathf.FloorToInt(loads.Count * .9f))] : 0f;
            TrafficFlow = Mathf.Clamp(100f - average * .8f - busy * .32f, 15f, 100f);
        }

        public float RoadTraffic(int x, int z) { return InBounds(x, z) ? Mathf.Clamp01(roadLoads[Index(x, z)] / 35f) : 0f; }

        [Serializable]
        class SaveData
        {
            public string Format;
            public int Version, MapSize, Day, PeakPopulation;
            public float Money, TaxRate, ElapsedDays, PendingDays;
            public CityTile[] Tiles;
        }
        public string SaveJson()
        {
            return JsonUtility.ToJson(new SaveData { Format = "seabright-city", Version = 2, MapSize = Size,
                Day = Day, PeakPopulation = PeakPopulation, Money = Money, TaxRate = TaxRate, ElapsedDays = elapsedDays, PendingDays = pendingDays, Tiles = Tiles });
        }
        public void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 3000000) throw new ArgumentException("Save file is empty or too large.");
            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception ex) { throw new ArgumentException("Save file is not valid JSON.", ex); }
            if (data == null || data.Format != "seabright-city" || (data.Version != 1 && data.Version != 2) || data.MapSize != Size || data.Tiles == null || data.Tiles.Length != Size * Size)
                throw new ArgumentException("This is not a supported Seabright save file.");
            if (data.Day < 1 || data.Day > 10000000 || !Finite(data.Money) || Mathf.Abs(data.Money) > 1000000000f ||
                !Finite(data.TaxRate) || data.TaxRate < 1f || data.TaxRate > 25f || !Finite(data.ElapsedDays) || data.ElapsedDays < 0f || data.ElapsedDays >= 1f ||
                !Finite(data.PendingDays) || data.PendingDays < 0f || data.PendingDays >= .1f)
                throw new ArgumentException("Save file has invalid city finances or date.");
            int savedPopulation = 0;
            for (int i = 0; i < data.Tiles.Length; i++)
            {
                var t = data.Tiles[i];
                if (t == null || t.X != i % Size || t.Z != i / Size || !Enum.IsDefined(typeof(TileKind), t.Kind) ||
                    (!IsLand(t.X, t.Z) && t.Kind != TileKind.Empty) || t.Level < 0 || t.Level > 4 ||
                    t.Residents < 0 || t.Residents > ResidentCapacity(t) ||
                    t.Jobs < 0 || t.Jobs > JobCapacity(t) || !Finite(t.Growth) || t.Growth < 0f || t.Growth > 1f ||
                    !Finite(t.LandValue) || !Finite(t.Pollution) ||
                    ((t.Kind == TileKind.Empty || t.Kind == TileKind.Road) && t.Level != 0) ||
                    (!IsZone(t.Kind) && t.Kind != TileKind.Empty && t.Kind != TileKind.Road && t.Level != 1))
                    throw new ArgumentException("Save file contains invalid map tiles.");
                if (data.Version == 1)
                {
                    if (t.Kind > TileKind.Clinic) throw new ArgumentException("Legacy save contains unsupported construction.");
                    t.AnchorX = t.AnchorZ = -1;
                }
                else if (t.Kind != TileKind.Stadium && (t.AnchorX != -1 || t.AnchorZ != -1))
                    throw new ArgumentException("Save file has invalid landmark reservations.");
                if (t.Kind == TileKind.Stadium)
                {
                    if (!InBounds(t.AnchorX, t.AnchorZ) || Mathf.Abs(t.X - t.AnchorX) > 1 || Mathf.Abs(t.Z - t.AnchorZ) > 1)
                        throw new ArgumentException("Save file has an invalid stadium footprint.");
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = t.AnchorX + dx, sz = t.AnchorZ + dz;
                            if (!InBounds(sx, sz)) throw new ArgumentException("Save file has an incomplete stadium footprint.");
                            var part = data.Tiles[Index(sx, sz)];
                            if (part == null || part.Kind != TileKind.Stadium || part.AnchorX != t.AnchorX || part.AnchorZ != t.AnchorZ)
                                throw new ArgumentException("Save file has an incomplete stadium footprint.");
                        }
                }
                savedPopulation += t.Residents;
            }
            if (data.Version == 1) data.PeakPopulation = savedPopulation;
            if (data.PeakPopulation < savedPopulation || data.PeakPopulation > Size * Size * 192)
                throw new ArgumentException("Save file has an invalid population milestone record.");
            // Commit only after every field and tile has passed validation.
            Tiles = data.Tiles; Day = data.Day; Money = data.Money; TaxRate = data.TaxRate; PeakPopulation = data.PeakPopulation; elapsedDays = data.ElapsedDays; pendingDays = data.PendingDays;
            Recalculate(); Revision++;
        }
    }
}
