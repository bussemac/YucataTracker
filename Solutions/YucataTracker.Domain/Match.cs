﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpArch.Domain.DomainModel;

namespace YucataTracker.Domain
{
	public class Match : Entity
	{

		protected Match()
		{
			this._players = new HashSet<Player>();
			this._matchResults = new HashSet<MatchResult>();
		}

		public Match(Game g)
			: this()
		{
			this.Game = g;
		}

		private ISet<Player> _players;
		private ISet<MatchResult> _matchResults;


		public virtual int YucataID { get; set; }
		public virtual Game Game { get; private set; }
		public virtual ISet<Player> Players { get { return _players; } }

		public virtual ISet<MatchResult> MatchResults { get { return _matchResults; } }

		public virtual bool UpdateMatchResults()
		{
			if (MatchResults.Count != Players.Count) return false;

			double MaxScore = MatchResults.Max(mr => mr.PrimaryScore);
			int NumTiedPlayers = MatchResults.Count(mr => mr.PrimaryScore == MaxScore);
			double TotalPoints = MatchResults.Sum(mr => mr.PrimaryScore);

			foreach (MatchResult res in MatchResults)
			{
				res.PointsShare = res.PrimaryScore / TotalPoints;
			}


			int itemCount = 0;
			MatchResult[] ordered = MatchResults.
				OrderBy(mr => (Game.PrimaryScoreSort == SortDirection.LowestFirst) ? mr.PrimaryScore : 1/mr.PrimaryScore).
				ThenBy(mr => (Game.FirstTiebreakerSort== SortDirection.LowestFirst) ? mr.TiebreakerScore : 1/mr.TiebreakerScore).
				ThenBy(mr => (Game.SecondTiebreakerSort == SortDirection.LowestFirst) ? mr.SecondaryTiebreakerScore : 1/mr.SecondaryTiebreakerScore).ToArray();
			
			
			int rank = 1;
			MatchResult prev = ordered[0];
			prev.Rank = 1;
			foreach (MatchResult mr in ordered.Skip(1))
			{
				itemCount++;
				 
				if (mr.PrimaryScore != prev.PrimaryScore || mr.TiebreakerScore != prev.TiebreakerScore || mr.SecondaryTiebreakerScore != prev.SecondaryTiebreakerScore)
				{
					rank = itemCount;
				}
				mr.Rank = rank;
				prev = mr;

				//TODO, optimization for next step: build IDictionary<int, List<MatchResult>> of rank => players with that rank, iterate over that instead of
				//using LINQ to query the matchresults again. 
			}

			//now the items are ranked, possibly including ties. We have to figure out how many tournament points to give 
			//assume: first place gets points = # players, second = first -1, etc. ties split all points evenly,
			//e.g., tied second and third in a 4p game is (3 + 2) / 2 = 2.5 each

			
			//effectively this means we need 
			//(SUM FROM Players.Count + 1 - Rank TO Players.Count - Rank + Count(Players with this Rank)) OVER (COUNT(Players with this Rank))
			//the numerator is an arithmatic series with common difference 1 and a start value we can easily find and a # of items that's even easier


			foreach (int Rank in MatchResults.Select(m => m.Rank).Distinct())
			{
				List<MatchResult> PlayersWithThisRank = MatchResults.Where(mr => mr.Rank == Rank).ToList();
				
				int numPlayersWithThisRank = PlayersWithThisRank.Count;

				if (numPlayersWithThisRank == 1) {
					PlayersWithThisRank[0].TournamentPoints = Players.Count + 1 - Rank; 
					continue; 
				}
				

				double sequenceSum = 0.5 * numPlayersWithThisRank * (2 * (Players.Count + 1 - Rank) + (numPlayersWithThisRank - 1) * 1);
				double earnedPoints = sequenceSum / numPlayersWithThisRank;
				foreach (MatchResult mr in PlayersWithThisRank)
				{
					mr.TournamentPoints = earnedPoints;
				}
			}

			return true;
		}
	}
}
