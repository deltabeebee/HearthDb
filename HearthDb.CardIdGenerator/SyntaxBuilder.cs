#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HearthDb.Enums;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

#endregion

namespace HearthDb.CardIdGenerator
{
	internal class SyntaxBuilder
	{
		private static Dictionary<string, List<string>> _namingConflicts = new Dictionary<string, List<string>>();

		internal static ClassDeclarationSyntax GetNonCollectible()
		{
			while(true)
			{
				var newNamingConflicts = new Dictionary<string, List<string>>();
				var classDecl = ClassDeclaration("NonCollectible").AddModifiers(Token(PublicKeyword));
				foreach(var c in Enum.GetNames(typeof(CardClass)))
				{
					var className = c == "DREAM" ? "DreamCards" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c.ToLower());
					var cCard = ClassDeclaration(className).AddModifiers(Token(PublicKeyword));
					var anyCards = false;
					foreach(var card in
						Cards.All.OrderBy(x => x.Value.Set)
							 .ThenBy(x => x.Key)
							 .Select(x => x.Value)
							 .Where(x => !x.Collectible && x.Class.ToString().Equals(c)))
					{
						var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(card.Name.ToLower());
						name = Regex.Replace(name, @"[^\w\d]", "");
						name = ResolveNameFromId(card, name);
						name = ResolveNamingConflict(name, card, newNamingConflicts);
						cCard = cCard.AddMembers(GenerateConst(name, card.Id));
						anyCards = true;
					}
					if(anyCards)
						classDecl = classDecl.AddMembers(cCard);
				}
				if(newNamingConflicts.Count(x => x.Value.Count > 1) < 5)
					return classDecl;
				_namingConflicts = newNamingConflicts.Where(x => x.Value.Count > 1).ToDictionary(pair => pair.Key, pair => pair.Value);
			}
		}

		private static string ResolveNameFromId(Card card, string name)
		{
			if(Regex.IsMatch(card.Id, @"_\d+[abhHt]?[eo]"))
				name += "Enchantment";
			if(Regex.IsMatch(card.Id, @"_\d+[hH]?[t]"))
				name += "Token";
			if(Helper.SpecialPrefixes.ContainsKey(card.Id))
				name += Helper.SpecialPrefixes[card.Id];
			if(Regex.IsMatch(card.Id, @"_2_TB$"))
				name += "TavernBrawlHeroPower";
			else if(Regex.IsMatch(card.Id, @"_TB$") || card.Id.StartsWith("TB"))
				name += "TavernBrawl";
			else if(card.Id == "BRM_027h")
				name += "Hero";
			else if(card.Id == "BRM_027p")
				name += "HeroPower";
			else if((Regex.IsMatch(card.Id, @"_[\dabet]+[hH]") || name.StartsWith("NAX1h")))
			{
				if(name.StartsWith("Heroic"))
					name = name.Substring(6);
				name += "Heroic";
			}
			if(Regex.IsMatch(name, @"^\d"))
				name = "_" + name;
			return name;
		}

		private static string ResolveNamingConflict(string name, Card card, Dictionary<string, List<string>> newNamingConflicts)
		{
			List<string> conflictingIds;
			if(_namingConflicts.TryGetValue(name, out conflictingIds))
			{
				if(conflictingIds.Any(x => x.Substring(0, 3) != card.Id.Substring(0, 3)))
					name += Helper.GetSetAbbreviation(card.Set);
				else
					name += (conflictingIds.IndexOf(card.Id) + 1).ToString();
			}
			else if(_namingConflicts.TryGetValue(name + Helper.GetSetAbbreviation(card.Set), out conflictingIds))
				name += Helper.GetSetAbbreviation(card.Set) + (conflictingIds.IndexOf(card.Id) + 1);
			List<string> ids;
			if(!newNamingConflicts.TryGetValue(name, out ids))
			{
				ids = new List<string>();
				newNamingConflicts.Add(name, ids);
			}
			ids.Add(card.Id);
			return name;
		}

		internal static ClassDeclarationSyntax GetCollectible(ClassDeclarationSyntax classDecl)
		{
			foreach(var c in Enum.GetNames(typeof(CardClass)))
			{
				var anyCards = false;
				var className = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c.ToLower());
				var cCard = ClassDeclaration(className).AddModifiers(Token(PublicKeyword));
				foreach(var card in
					Cards.All.Values.Where(x => x.Collectible && x.Class.ToString().Equals(c)))
				{
					var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(card.Name.ToLower());
					name = Regex.Replace(name, @"[^\w\d]", "");
					cCard = cCard.AddMembers(GenerateConst(name, card.Id));
					anyCards = true;
				}
				if(anyCards)
					classDecl = classDecl.AddMembers(cCard);
			}
			return classDecl;
		}

		internal static FieldDeclarationSyntax GenerateConst(string identifier, string value)
		{
			var assignedValue = EqualsValueClause(LiteralExpression(StringLiteralExpression, Literal(value)));
			var declaration = SeparatedList(new[] {VariableDeclarator(Identifier(identifier), null, assignedValue)});
			return
				FieldDeclaration(VariableDeclaration(ParseTypeName("string"), declaration))
					.AddModifiers(Token(PublicKeyword))
					.AddModifiers(Token(ConstKeyword));
		}
	}
}