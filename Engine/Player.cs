using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;

namespace Engine
{
    public class Player : LivingCreature
    {
        private int _gold;
        private int _experiencePoints;
        private Location _currentLocation;

        public event EventHandler<MessageEventArgs> OnMessage;

        public int Gold
        {
            get { return _gold; }
            set
            {
                _gold = value;
                OnPropertyChanged("Gold");
            }
        }

        public int ExperiencePoints
        {
            get { return _experiencePoints; }
            private set
            {
                _experiencePoints = value;
                OnPropertyChanged("ExperiencePoints");
                OnPropertyChanged("Level");
            }
        }

        public int Level
        {
            get { return ((ExperiencePoints / 100) + 1); }
        }

        public Location CurrentLocation
        {
            get { return _currentLocation; }
            set
            {
                _currentLocation = value;
                OnPropertyChanged("CurrentLocation");
            }
        }

        public Weapon CurrentWeapon { get; set; }

        public BindingList<InventoryItem> Inventory { get; set; }

        public BindingList<InventoryItem> SellableItems
        {
            get { return new BindingList<InventoryItem>(Inventory.Where(x => x.Price != World.UNSELLABLE_ITEM_PRICE).ToList()); }
        }

        public List<Weapon> Weapons
        {
            get { return Inventory.Where(x => x.Details is Weapon).Select(x => x.Details as Weapon).ToList(); }
        }

        public List<HealingPotion> Potions
        {
            get { return Inventory.Where(x => x.Details is HealingPotion).Select(x => x.Details as HealingPotion).ToList(); }
        }

        public BindingList<PlayerQuest> Quests { get; set; }

        private Monster _currentMonster { get; set; }

        private Player(int currentHitPoints, int maximumHitPoints, int gold, int experiencePoints) : base(currentHitPoints, maximumHitPoints)
        {
            Gold = gold;
            ExperiencePoints = experiencePoints;

            Inventory = new BindingList<InventoryItem>();
            Quests = new BindingList<PlayerQuest>();
        }

        public static Player CreateDefaultPlayer()
        {
            Player player = new Player(10, 10, 20, 0);
            player.Inventory.Add(new InventoryItem(World.ItemByID(World.ITEM_ID_RUSTY_SWORD), 1));
            player.CurrentLocation = World.LocationByID(World.LOCATION_ID_HOME);

            return player;
        }

        public static Player CreatePlayerFromXmlString(string xmlPlayerData)
        {
            try
            {
                XmlDocument playerData = new XmlDocument();

                playerData.LoadXml(xmlPlayerData);

                int currentHitPoints = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/CurrentHitPoints").InnerText);
                int maximumHitPoints = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/MaximumHitPoints").InnerText);
                int gold = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/Gold").InnerText);
                int experiencePoints = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/ExperiencePoints").InnerText);

                Player player = new Player(currentHitPoints, maximumHitPoints, gold, experiencePoints);

                int currentLocationID = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/CurrentLocation").InnerText);
                player.CurrentLocation = World.LocationByID(currentLocationID);

                if (playerData.SelectSingleNode("/Player/Stats/CurrentWeapon") != null)
                {
                    int currentWeaponID = Convert.ToInt32(playerData.SelectSingleNode("/Player/Stats/CurrentWeapon").InnerText);
                    player.CurrentWeapon = (Weapon)World.ItemByID(currentWeaponID);
                }

                foreach (XmlNode node in playerData.SelectNodes("/Player/InventoryItems/InventoryItem"))
                {
                    int id = Convert.ToInt32(node.Attributes["ID"].Value);
                    int quantity = Convert.ToInt32(node.Attributes["Quantity"].Value);

                    for (int i = 0; i < quantity; i++)
                    {
                        player.AddItemToInventory(World.ItemByID(id));
                    }
                }

                foreach (XmlNode node in playerData.SelectNodes("/Player/PlayerQuests/PlayerQuest"))
                {
                    int id = Convert.ToInt32(node.Attributes["ID"].Value);
                    bool isCompleted = Convert.ToBoolean(node.Attributes["IsCompleted"].Value);

                    PlayerQuest playerQuest = new PlayerQuest(World.QuestByID(id));
                    playerQuest.IsCompleted = isCompleted;

                    player.Quests.Add(playerQuest);
                }

                return player;
            }
            catch
            {
                // If there was an error with the XML data, return a default player object
                return Player.CreateDefaultPlayer();
            }
        }

        public void MoveTo(Location newLocation)
        {
            if(PlayerDoesNotHaveTheRequiredItemToEnter(newLocation))
            {
                RaiseMessage("You must have a " + newLocation.ItemRequiredToEnter.Name + " to enter this location.");
                return;
            }

            // The player can enter this location
            CurrentLocation = newLocation;

            CompletelyHeal();

            if(newLocation.HasAQuest)
            {
                if(PlayerDoesNotHaveThisQuest(newLocation.QuestAvailableHere))
                {
                    GiveQuestToPlayer(newLocation.QuestAvailableHere);
                }
                else
                {
                    if(PlayerHasNotCompleted(newLocation.QuestAvailableHere) &&
                        PlayerHasAllQuestCompletionItemsFor(newLocation.QuestAvailableHere))
                    {
                        GivePlayerQuestRewards(newLocation.QuestAvailableHere);
                    }
                }
            }

            SetTheCurrentMonsterForTheCurrentLocation(newLocation);
        }

        public void MoveNorth()
        {
            if (CurrentLocation.LocationToNorth != null)
            {
                MoveTo(CurrentLocation.LocationToNorth);
            }
        }

        public void MoveEast()
        {
            if (CurrentLocation.LocationToEast != null)
            {
                MoveTo(CurrentLocation.LocationToEast);
            }
        }

        public void MoveSouth()
        {
            if (CurrentLocation.LocationToSouth != null)
            {
                MoveTo(CurrentLocation.LocationToSouth);
            }
        }

        public void MoveWest()
        {
            if (CurrentLocation.LocationToWest != null)
            {
                MoveTo(CurrentLocation.LocationToWest);
            }
        }

        public void UseWeapon(Weapon weapon)
        {
            int damage = RandomNumberGenerator.NumberBetween(weapon.MinimumDamage, weapon.MaximumDamage);

            if(damage == 0)
            {
                RaiseMessage("You missed the " + _currentMonster.Name);
            }
            else
            {
                _currentMonster.CurrentHitPoints -= damage;
                RaiseMessage("You hit the " + _currentMonster.Name + " for " + damage + " points.");
            }

            if(_currentMonster.IsDead)
            {
                LootTheCurrentMonster();

                // "Move" to the current location, to refresh the current monster
                MoveTo(CurrentLocation);
            }
            else
            {
                LetTheMonsterAttack();
            }
        }

        private void LootTheCurrentMonster()
        {
            RaiseMessage("");
            RaiseMessage("You defeated the " + _currentMonster.Name);
            RaiseMessage("You receive " + _currentMonster.RewardExperiencePoints + " experience points");
            RaiseMessage("You receive " + _currentMonster.RewardGold + " gold");

            AddExperiencePoints(_currentMonster.RewardExperiencePoints);
            Gold += _currentMonster.RewardGold;

            // Give the monster's loot items to the player
            foreach(InventoryItem inventoryItem in _currentMonster.LootItems)
            {
                AddItemToInventory(inventoryItem.Details);

                RaiseMessage(string.Format("You loot {0} {1}", inventoryItem.Quantity, inventoryItem.Description));
            }

            RaiseMessage("");
        }

        public void UsePotion(HealingPotion potion)
        {
            RaiseMessage("You drink a " + potion.Name);

            HealPlayer(potion.AmountToHeal);

            RemoveItemFromInventory(potion);

            // The player used their turn to drink the potion, so let the monster attack now
            LetTheMonsterAttack();
        }

        public void AddItemToInventory(Item itemToAdd, int quantity = 1)
        {
            InventoryItem existingItemInInventory = Inventory.SingleOrDefault(ii => ii.Details.ID == itemToAdd.ID);

            if (existingItemInInventory == null)
            {
                // They didn't have the item, so add it to their inventory
                Inventory.Add(new InventoryItem(itemToAdd, quantity));
            }
            else
            {
                // They have the item in their inventory, so increase the quantity
                existingItemInInventory.Quantity += quantity;
            }

            RaiseInventoryChangedEvent(itemToAdd);
        }

        public void RemoveItemFromInventory(Item itemToRemove, int quantity = 1)
        {
            InventoryItem item = Inventory.SingleOrDefault(ii => ii.Details.ID == itemToRemove.ID);

            if(item != null)
            {
                item.Quantity -= quantity;

                if(item.Quantity == 0)
                {
                    Inventory.Remove(item);
                }

                RaiseInventoryChangedEvent(itemToRemove);
            }
        }

        public string ToXmlString()
        {
            XmlDocument playerData = new XmlDocument();

            // Create the top-level XML node
            XmlNode player = playerData.CreateElement("Player");
            playerData.AppendChild(player);

            // Create the "Stats" child node to hold the other player statistics nodes
            XmlNode stats = playerData.CreateElement("Stats");
            player.AppendChild(stats);

            // Create the child nodes for the "Stats" node
            CreateNewChildXmlNode(playerData, stats, "CurrentHitPoints", CurrentHitPoints);
            CreateNewChildXmlNode(playerData, stats, "MaximumHitPoints", MaximumHitPoints);
            CreateNewChildXmlNode(playerData, stats, "Gold", Gold);
            CreateNewChildXmlNode(playerData, stats, "ExperiencePoints", ExperiencePoints);
            CreateNewChildXmlNode(playerData, stats, "CurrentLocation", CurrentLocation.ID);

            if (CurrentWeapon != null)
            {
                CreateNewChildXmlNode(playerData, stats, "CurrentWeapon", CurrentWeapon.ID);
            }

            // Create the "InventoryItems" child node to hold each InventoryItem node
            XmlNode inventoryItems = playerData.CreateElement("InventoryItems");
            player.AppendChild(inventoryItems);

            // Create an "InventoryItem" node for each item in the player's inventory
            foreach (InventoryItem item in Inventory)
            {
                XmlNode inventoryItem = playerData.CreateElement("InventoryItem");

                AddXmlAttributeToNode(playerData, inventoryItem, "ID", item.Details.ID);
                AddXmlAttributeToNode(playerData, inventoryItem, "Quantity", item.Quantity);

                inventoryItems.AppendChild(inventoryItem);
            }

            // Create the "PlayerQuests" child node to hold each PlayerQuest node
            XmlNode playerQuests = playerData.CreateElement("PlayerQuests");
            player.AppendChild(playerQuests);

            // Create a "PlayerQuest" node for each quest the player has acquired
            foreach (PlayerQuest quest in this.Quests)
            {
                XmlNode playerQuest = playerData.CreateElement("PlayerQuest");

                AddXmlAttributeToNode(playerData, playerQuest, "ID", quest.Details.ID);
                AddXmlAttributeToNode(playerData, playerQuest, "IsCompleted", quest.IsCompleted);

                playerQuests.AppendChild(playerQuest);
            }

            return playerData.InnerXml; // The XML document, as a string, so we can save the data to disk
        }

        private bool HasRequiredItemToEnterThisLocation(Location location)
        {
            if (location.ItemRequiredToEnter == null)
            {
                // There is no required item for this location, so return "true"
                return true;
            }

            // See if the player has the required item in their inventory
            return Inventory.Any(ii => ii.Details.ID == location.ItemRequiredToEnter.ID);
        }

        private void SetTheCurrentMonsterForTheCurrentLocation(Location location)
        {
            // Populate the current monster with this location's monster (or null, if there is no monster here)
            _currentMonster = location.NewInstanceOfMonsterLivingHere();

            if(_currentMonster != null)
            {
                RaiseMessage("You see a " + location.MonsterLivingHere.Name);
            }
        }

        private bool PlayerDoesNotHaveTheRequiredItemToEnter(Location location)
        {
            return !HasRequiredItemToEnterThisLocation(location);
        }

        private bool PlayerDoesNotHaveThisQuest(Quest quest)
        {
            return Quests.All(pq => pq.Details.ID != quest.ID);
        }

        private bool PlayerHasNotCompleted(Quest quest)
        {
            return Quests.Any(pq => pq.Details.ID == quest.ID && !pq.IsCompleted);
        }

        private void GiveQuestToPlayer(Quest quest)
        {
            RaiseMessage("You receive the " + quest.Name + " quest.");
            RaiseMessage(quest.Description);
            RaiseMessage("To complete it, return with:");

            foreach(QuestCompletionItem qci in quest.QuestCompletionItems)
            {
                RaiseMessage(string.Format("{0} {1}", qci.Quantity,
                    qci.Quantity == 1 ? qci.Details.Name : qci.Details.NamePlural));
            }

            RaiseMessage("");

            Quests.Add(new PlayerQuest(quest));
        }

        private bool PlayerHasAllQuestCompletionItemsFor(Quest quest)
        {
            // See if the player has all the items needed to complete the quest here
            foreach(QuestCompletionItem qci in quest.QuestCompletionItems)
            {
                // Check each item in the player's inventory, to see if they have it, and enough of it
                if(!Inventory.Any(ii => ii.Details.ID == qci.Details.ID && ii.Quantity >= qci.Quantity))
                {
                    return false;
                }
            }

            // If we got here, then the player must have all the required items, and enough of them, to complete the quest.
            return true;
        }

        private void RemoveQuestCompletionItems(Quest quest)
        {
            foreach (QuestCompletionItem qci in quest.QuestCompletionItems)
            {
                // Subtract the quantity from the player's inventory that was needed to complete the quest
                InventoryItem item = Inventory.SingleOrDefault(ii => ii.Details.ID == qci.Details.ID);

                if (item != null)
                {
                    RemoveItemFromInventory(item.Details, qci.Quantity);
                }
            }
        }

        private void AddExperiencePoints(int experiencePointsToAdd)
        {
            ExperiencePoints += experiencePointsToAdd;
            MaximumHitPoints = (Level * 10);
        }

        private void GivePlayerQuestRewards(Quest quest)
        {
            RaiseMessage("");
            RaiseMessage("You complete the '" + quest.Name + "' quest.");
            RaiseMessage("You receive: ");
            RaiseMessage(quest.RewardExperiencePoints + " experience points");
            RaiseMessage(quest.RewardGold + " gold");
            RaiseMessage(quest.RewardItem.Name, true);

            AddExperiencePoints(quest.RewardExperiencePoints);
            Gold += quest.RewardGold;

            RemoveQuestCompletionItems(quest);
            AddItemToInventory(quest.RewardItem);

            MarkPlayerQuestCompleted(quest);
        }

        public void MarkPlayerQuestCompleted(Quest quest)
        {
            // Find the quest in the player's quest list
            PlayerQuest playerQuest = Quests.SingleOrDefault(pq => pq.Details.ID == quest.ID);

            if (playerQuest != null)
            {
                playerQuest.IsCompleted = true;
            }
        }

        private void LetTheMonsterAttack()
        {
            int damageToPlayer = RandomNumberGenerator.NumberBetween(0, _currentMonster.MaximumDamage);

            RaiseMessage("The " + _currentMonster.Name + " did " + damageToPlayer + " points of damage.");

            CurrentHitPoints -= damageToPlayer;

            if(IsDead)
            {
                RaiseMessage("The " + _currentMonster.Name + " killed you.");

                MoveHome();
            }
        }

        private void HealPlayer(int hitPointsToHeal)
        {
            CurrentHitPoints = Math.Min(CurrentHitPoints + hitPointsToHeal, MaximumHitPoints);
        }

        private void CompletelyHeal()
        {
            CurrentHitPoints = MaximumHitPoints;
        }

        public bool HasThisQuest(Quest quest)
        {
            return Quests.Any(pq => pq.Details.ID == quest.ID);
        }

        public bool CompletedThisQuest(Quest quest)
        {
            foreach (PlayerQuest playerQuest in Quests)
            {
                if (playerQuest.Details.ID == quest.ID)
                {
                    return playerQuest.IsCompleted;
                }
            }

            return false;
        }

        private void MoveHome()
        {
            MoveTo(World.LocationByID(World.LOCATION_ID_HOME));
        }

        private void CreateNewChildXmlNode(XmlDocument document, XmlNode parentNode, string elementName, object value)
        {
            XmlNode node = document.CreateElement(elementName);
            node.AppendChild(document.CreateTextNode(value.ToString()));
            parentNode.AppendChild(node);
        }

        private void AddXmlAttributeToNode(XmlDocument document, XmlNode node, string attributeName, object value)
        {
            XmlAttribute attribute = document.CreateAttribute(attributeName);
            attribute.Value = value.ToString();
            node.Attributes.Append(attribute);
        }

        private void RaiseInventoryChangedEvent(Item item)
        {
            if (item is Weapon)
            {
                OnPropertyChanged("Weapons");
            }

            if (item is HealingPotion)
            {
                OnPropertyChanged("Potions");
            }
        }

        private void RaiseMessage(string message, bool addExtraNewLine = false)
        {
            if (OnMessage != null)
            {
                OnMessage(this, new MessageEventArgs(message, addExtraNewLine));
            }
        }
    }
}