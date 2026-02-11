using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql.PostgresTypes;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.Schema;
using PostgresType = DatabaseDesigner.DBDesigner.PostgresType;


namespace DatabaseDesigner
{

    public static class DBDesignerTest
    {
        // ========= GENERATED TEST TABLE CREATORS =========

        // Schema 1: Yorha Androids
        public static DatabaseDesign CreateOperatorTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("codename", "Operator codename", postgresType: PostgresType.Text, isUnique: true, isNotNull: true),
                new RowOptions("rank", "Operator rank", postgresType: PostgresType.Text),
                new RowOptions("role", "Combat role", postgresType: PostgresType.Text),
                new RowOptions("is_active", "Active status", postgresType: PostgresType.Boolean, defaultValue: "true", defaultIsKeyword: true)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".operator", "OperatorCodenameIndex", new[] { "codename" }, IndexType.Unique)
            };



            // Correcting the line to properly instantiate the DatabaseDesigner class  


            return DBDesigner.DatabaseDesigner($"{schema}.operator", "Yorha operator roster", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateMissionTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("name", "Mission name", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("description", "Mission description", postgresType: PostgresType.Text),
                new RowOptions("operator_id", "Assigned operator", postgresType: PostgresType.Uuid),
                new RowOptions("start_date", "Mission start date", postgresType: PostgresType.Timestamp),
                new RowOptions("end_date", "Mission end date", postgresType: PostgresType.Timestamp),
                new RowOptions("is_completed", "Completion status", postgresType: PostgresType.Boolean, defaultValue: "false", defaultIsKeyword: true)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".mission", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".mission", "IsCompletedIndex", new[] { "is_completed" }, IndexType.Basic)
            };


            return DBDesigner.DatabaseDesigner($"{schema}.mission", "Mission details", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateBlackBoxTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("operator_id", "Related operator", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("log_data", "Encrypted log data", isEncrypted: true),
                new RowOptions("timestamp", "Record timestamp", postgresType: PostgresType.Timestamp, isNotNull: true)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".black_box", schema + ".operator", "operator_id", "id", ReferentialAction.Restrict, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".black_box", "BlackBox Index", new[] { "timestamp" }, IndexType.Basic)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.black_box", "Android black box data", rows, null, references, indexes);
        }

        public static DatabaseDesign CreatePodTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("model", "Pod model type", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("operator_id", "Associated operator", postgresType: PostgresType.Uuid),
                new RowOptions("is_deployed", "Deployment status", postgresType: PostgresType.Boolean, defaultValue: "false", defaultIsKeyword: true)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".pod", schema + ".operator", "operator_id", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.pod", "Support Pod details", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateFlightUnitTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("unit_code", "Flight unit code", postgresType: PostgresType.Text, isUnique: true, isNotNull: true),
                new RowOptions("status", "Current status", postgresType: PostgresType.Text),
                new RowOptions("last_maintenance", "Date of last maintenance", postgresType: PostgresType.Date)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".flight_unit", "FlightUnitCodeIndex", new[] { "unit_code" }, IndexType.Unique)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.flight_unit", "Flight unit operational data", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateCombatLogTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("operator_id", "Operator involved", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("mission_id", "Mission associated", postgresType: PostgresType.Uuid),
                new RowOptions("event_time", "Event timestamp", postgresType: PostgresType.Timestamp, isNotNull: true),
                new RowOptions("event_data", "Detailed JSON event data", postgresType: PostgresType.Jsonb)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".combat_log", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                new ReferenceOptions(schema + ".combat_log", schema + ".mission", "mission_id", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".combat_log", "CombatLogIndex", new[] { "event_time" }, IndexType.Basic)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.combat_log", "Combat event logs", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateMemoryChipTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("operator_id", "Operator owner", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("chip_data", "Encrypted chip data", isEncrypted: true),
                new RowOptions("last_update", "Last update timestamp", postgresType: PostgresType.Timestamp)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".memory_chip", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>();
            return DBDesigner.DatabaseDesigner($"{schema}.memory_chip", "Android memory chip data", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateMaintenanceLogTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("component", "Component serviced", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("performed_by", "Technician name", postgresType: PostgresType.Text),
                new RowOptions("date", "Service date", postgresType: PostgresType.Date, isNotNull: true),
                new RowOptions("notes", "Service notes", postgresType: PostgresType.Text)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.maintenance_log", "Maintenance records", rows, null, references, indexes);
        }

        // --- Machines schema tables ---

        public static DatabaseDesign CreateEnemyTypeTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("name", "Enemy type name", postgresType: PostgresType.Text, isUnique: true, isNotNull: true),
                new RowOptions("description", "Description of enemy type", postgresType: PostgresType.Text)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".enemy_type", null, new[] { "name" }, IndexType.Basic)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.enemy_type", "Types of enemy machines", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateMachineNetworkTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("network_id", "Network identifier", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("nodes", "Machine nodes in network", postgresType: PostgresType.Jsonb)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.machine_network", "Machine network data", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateBehaviorPatternTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("pattern_name", "Behavior pattern name", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("description", "Description of pattern", postgresType: PostgresType.Text),
                new RowOptions("pattern_data", "Serialized AI model data", postgresType: PostgresType.Jsonb)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".behavior_pattern", null, new[] { "pattern_data" }, IndexType.Gin)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.behavior_pattern", "AI behavior patterns", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateEvolutionDataTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("machine_id", "Related machine", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("evolution_stage", "Stage of evolution", postgresType: PostgresType.Integer, isNotNull: true),
                new RowOptions("change_log", "Evolution changes", postgresType: PostgresType.Text)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".evolution_data", schema + ".enemy_type", "machine_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.evolution_data", "Machine evolution data", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateHackingAttemptTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("target_machine_id", "Target machine", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("attempt_time", "Timestamp of attempt", postgresType: PostgresType.Timestamp, isNotNull: true),
                new RowOptions("success", "Attempt success", postgresType: PostgresType.Boolean, defaultValue: "false", defaultIsKeyword: true),
                new RowOptions("log_data", "Attempt logs", postgresType: PostgresType.Text)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".hacking_attempt", schema + ".enemy_type", "target_machine_id", "id", ReferentialAction.Restrict, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.hacking_attempt", "Hacking attempt logs", rows, null, references, indexes);
        }

        // --- Weapons schema tables ---

        public static DatabaseDesign CreateWeaponTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("name", "Weapon name", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("weapon_type", "Type of weapon", postgresType: PostgresType.Text),
                new RowOptions("damage", "Damage rating", postgresType: PostgresType.Integer),
                new RowOptions("weight", "Weapon weight", postgresType: PostgresType.Numeric),
                new RowOptions("pattern_json", "Attack pattern data", postgresType: PostgresType.Jsonb)
            };
            var references = new List<ReferenceOptions>();
            var indexes = new List<IndexDefinition>
            {
                new IndexDefinition(schema + ".weapon", null, new[] { "pattern_json" }, IndexType.Gin)
            };

            return DBDesigner.DatabaseDesigner($"{schema}.weapon", "Weapon details", rows, null, references, indexes);
        }

        public static DatabaseDesign CreateUpgradeTable(string schema)
        {
            var rows = new List<RowOptions>
            {
                new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
                new RowOptions("weapon_id", "Associated weapon", postgresType: PostgresType.Uuid, isNotNull: true),
                new RowOptions("upgrade_name", "Upgrade name", postgresType: PostgresType.Text, isNotNull: true),
                new RowOptions("description", "Upgrade description", postgresType: PostgresType.Text)
            };
            var references = new List<ReferenceOptions>
            {
                new ReferenceOptions(schema + ".upgrade", schema + ".weapon", "weapon_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
            };
            var indexes = new List<IndexDefinition>();

            return DBDesigner.DatabaseDesigner($"{schema}.upgrade", "Weapon upgrade details", rows, null, references, indexes);
        }


        // Weapons schema
        public static DatabaseDesign CreateAttackPatternTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("pattern_name", "Pattern name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("pattern_data", "Pattern data JSON", postgresType: PostgresType.Jsonb)
    };
            var indexes = new List<IndexDefinition>
    {
        new IndexDefinition(schema + ".attack_pattern", null, new[] { "pattern_data" }, IndexType.Gin)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.attack_pattern", "Attack pattern details", rows, null, new List<ReferenceOptions>(), indexes);
        }

        public static DatabaseDesign CreateCraftingMaterialTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("material_name", "Material name", postgresType: PostgresType.Text, isUnique: true, isNotNull: true),
        new RowOptions("rarity", "Material rarity", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.crafting_material", "Materials for crafting", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        // World Data schema
        public static DatabaseDesign CreateLocationTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("name", "Location name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("coordinates", "Geo coordinates", postgresType: PostgresType.Point),
        new RowOptions("description", "Location description", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.location", "World locations", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateEnvironmentHazardTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("hazard_type", "Type of hazard", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("severity", "Severity level", postgresType: PostgresType.Integer),
        new RowOptions("area", "Affected area coords", postgresType: null, customType: "geometry"),
        new RowOptions("description", "Hazard description", postgresType: PostgresType.Text)
    };
            var customRows = new[] { "CONSTRAINT chk_severity_nonnegative CHECK (severity >= 0)" };

            return DBDesigner.DatabaseDesigner($"{schema}.environment_hazard", "Environmental hazards in the world", rows, customRows, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateResourceNodeTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("resource_type", "Type of resource", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("quantity", "Quantity available", postgresType: PostgresType.Integer),
        new RowOptions("location", "Node coordinates", postgresType: PostgresType.Point),
        new RowOptions("regeneration_rate", "Regeneration rate", postgresType: PostgresType.Real)
    };
            var customRows = new[] { "CONSTRAINT chk_quantity_nonnegative CHECK (quantity >= 0)" };

            return DBDesigner.DatabaseDesigner($"{schema}.resource_node", "Resource nodes scattered in the world", rows, customRows, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateWeatherPatternTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("pattern_name", "Weather pattern name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("duration_minutes", "Duration in minutes", postgresType: PostgresType.Integer),
        new RowOptions("effects", "Effects on environment", postgresType: PostgresType.Text),
        new RowOptions("frequency", "Occurrence frequency", postgresType: PostgresType.Real)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.weather_pattern", "Weather patterns impacting gameplay", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateFastTravelTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("location_name", "Fast travel location name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("coordinates", "Fast travel coords", postgresType: PostgresType.Point),
        new RowOptions("unlocked", "Unlocked status", postgresType: PostgresType.Boolean, defaultValue: "false", defaultIsKeyword: true)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.fast_travel", "Fast travel points in the world", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        // Resistance schema
        public static DatabaseDesign CreateResistanceMemberTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("name", "Member full name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("role", "Member role", postgresType: PostgresType.Text),
        new RowOptions("rank", "Member rank", postgresType: PostgresType.Integer),
        new RowOptions("last_active", "Last active timestamp", postgresType: PostgresType.Timestamp)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.resistance_member", "Members of the resistance", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateOutpostTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("outpost_name", "Outpost name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("location", "Outpost coordinates", postgresType: PostgresType.Point),
        new RowOptions("capacity", "Max members capacity", postgresType: PostgresType.Integer)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.outpost", "Resistance outposts", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateSupplyCacheTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("cache_name", "Cache name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("location", "Cache coordinates", postgresType: PostgresType.Point),
        new RowOptions("items_stored", "Stored items description", postgresType: PostgresType.Text),
        new RowOptions("last_restocked", "Last restocked timestamp", postgresType: PostgresType.Timestamp)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.supply_cache", "Supply caches used by resistance", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateEmergencyCommsTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("comms_id", "Comms device ID", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("location", "Comms coordinates", postgresType: PostgresType.Point),
        new RowOptions("status", "Device status", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.emergency_comms", "Emergency communication points", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        // Pascal's Village schema
        public static DatabaseDesign CreateVillagerTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("name", "Villager full name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("age", "Villager age", postgresType: PostgresType.Integer),
        new RowOptions("occupation", "Occupation or role", postgresType: PostgresType.Text),
        new RowOptions("relationship_status", "Relationship status", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.villager", "Villagers in Pascal's village", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateTradeHistoryTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("villager_id", "Villager ID", postgresType: PostgresType.Uuid, isNotNull: true),
        new RowOptions("item_traded", "Traded item description", postgresType: PostgresType.Text),
        new RowOptions("trade_date", "Trade timestamp", postgresType: PostgresType.Timestamp),
        new RowOptions("trade_value", "Trade value", postgresType: PostgresType.Numeric)
    };
            var references = new List<ReferenceOptions>
    {
        new ReferenceOptions(schema + ".trade_history", schema + ".villager", "villager_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.trade_history", "Trade history records", rows, null, references, new List<IndexDefinition>());
        }

        public static DatabaseDesign CreatePhilosophyLogTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("entry_date", "Entry timestamp", postgresType: PostgresType.Timestamp, isNotNull: true),
        new RowOptions("author", "Author name", postgresType: PostgresType.Text),
        new RowOptions("content", "Philosophical content", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.philosophy_log", "Logs of philosophical discussions", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateMachineTreatyTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("treaty_name", "Treaty name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("signed_date", "Date signed", postgresType: PostgresType.Date),
        new RowOptions("terms", "Treaty terms", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.machine_treaty", "Machine treaty agreements", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        // Tower schema
        public static DatabaseDesign CreateTowerFloorTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("floor_number", "Floor number", postgresType: PostgresType.Integer, isNotNull: true),
        new RowOptions("difficulty_level", "Difficulty rating", postgresType: PostgresType.Integer),
        new RowOptions("description", "Floor description", postgresType: PostgresType.Text)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.tower_floor", "Tower floors", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateBossEncounterTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("floor_id", "Linked floor id", postgresType: PostgresType.Uuid, isNotNull: true),
        new RowOptions("boss_name", "Boss name", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("health_points", "Boss HP", postgresType: PostgresType.Integer),
        new RowOptions("reward", "Reward for defeating", postgresType: PostgresType.Text)
    };
            var references = new List<ReferenceOptions>
    {
        new ReferenceOptions($"{schema}.boss_encounter", $"{schema}.tower_floor", "floor_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.boss_encounter", "Boss encounters on tower floors", rows, null, references, new List<IndexDefinition>());
        }

        public static DatabaseDesign CreateTerminalAccessTable(string schema)
        {
            var rows = new List<RowOptions>
    {
        new RowOptions("id", "Primary key", postgresType: PostgresType.Uuid, isPrimary: true),
        new RowOptions("user_id", "User accessing terminal", postgresType: PostgresType.Uuid, isNotNull: true),
        new RowOptions("terminal_id", "Terminal identifier", postgresType: PostgresType.Text, isNotNull: true),
        new RowOptions("access_time", "Access timestamp", postgresType: PostgresType.Timestamp, isNotNull: true),
        new RowOptions("access_granted", "Whether access was granted", postgresType: PostgresType.Boolean)
    };

            return DBDesigner.DatabaseDesigner($"{schema}.terminal_access", "Tower terminal access logs", rows, null, new List<ReferenceOptions>(), new List<IndexDefinition>());
        }






    }


}
