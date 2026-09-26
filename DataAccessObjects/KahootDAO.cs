using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects
{
    public class KahootDAO
    {
        private static KahootDAO? instance;
        private static readonly object instanceLock = new();

        private KahootDAO()
        {
        }

        public static KahootDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    instance ??= new KahootDAO();
                    return instance;
                }
            }
        }

        public KahootRoom CreateRoom(QuizManagementDbContext context, KahootRoom room)
        {
            context.KahootRooms.Add(room);
            context.SaveChanges();
            return room;
        }

        public KahootRoom? GetRoomByCode(QuizManagementDbContext context, string roomCode)
        {
            return context.KahootRooms
                .Include(r => r.Deck)
                .Include(r => r.HostUser)
                .Include(r => r.Participants)
                .FirstOrDefault(r => r.RoomCode == roomCode);
        }

        public KahootRoom? GetRoomById(QuizManagementDbContext context, int id)
        {
            return context.KahootRooms
                .Include(r => r.Deck)
                .Include(r => r.Participants)
                .FirstOrDefault(r => r.Id == id);
        }

        public KahootParticipant AddParticipant(QuizManagementDbContext context, KahootParticipant participant)
        {
            context.KahootParticipants.Add(participant);
            context.SaveChanges();
            return participant;
        }

        public KahootParticipant? GetParticipant(QuizManagementDbContext context, int participantId)
        {
            return context.KahootParticipants
                .Include(p => p.Room)
                .FirstOrDefault(p => p.Id == participantId);
        }

        public List<KahootParticipant> GetParticipantsByRoom(QuizManagementDbContext context, string roomCode)
        {
            return context.KahootParticipants
                .Where(p => p.Room.RoomCode == roomCode)
                .OrderByDescending(p => p.TotalScore)
                .ThenBy(p => p.JoinedAt)
                .ToList();
        }

        public void SaveAnswer(QuizManagementDbContext context, KahootAnswer answer)
        {
            context.KahootAnswers.Add(answer);
            
            var participant = context.KahootParticipants.FirstOrDefault(p => p.Id == answer.ParticipantId);
            if (participant != null)
            {
                participant.TotalScore += answer.ScoreEarned;
            }

            context.SaveChanges();
        }

        public void UpdateRoomStatus(QuizManagementDbContext context, string roomCode, string status, int currentQuestionIndex)
        {
            var room = context.KahootRooms.FirstOrDefault(r => r.RoomCode == roomCode);
            if (room != null)
            {
                room.Status = status;
                room.CurrentQuestionIndex = currentQuestionIndex;
                context.SaveChanges();
            }
        }

        public List<Question> GetQuestionsForDeck(QuizManagementDbContext context, int deckId)
        {
            return context.Questions
                .Include(q => q.Answers)
                .Where(q => q.DeckId == deckId)
                .AsNoTracking()
                .ToList();
        }
    }
}
