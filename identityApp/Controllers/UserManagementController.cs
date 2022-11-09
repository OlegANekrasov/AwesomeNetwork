using AutoMapper;
using AwesomeNetwork.Data;
using AwesomeNetwork.Data.Repository;
using AwesomeNetwork.Data.UoW;
using AwesomeNetwork.Extentions;
using AwesomeNetwork.Hubs;
using AwesomeNetwork.Models.Users;
using AwesomeNetwork.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AwesomeNetwork.Controllers
{
    public class UserManagementController : Controller
    {
        private IMapper _mapper;
        private readonly UserManager<IdentityUser> _userManager;
        private IUnitOfWork _unitOfWork;
        private IHubContext<ChatHub> _hubContext;

        public UserManagementController(UserManager<IdentityUser> userManager, IMapper mapper, IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext)
        {
            _userManager = userManager;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _hubContext = hubContext;
        }


        [Route("Generate")]
        [HttpGet]
        public async Task<IActionResult> Generate()
        {

            var usergen = new GenetateUsers();
            var userlist = usergen.Populate(35);

            foreach (var user in userlist)
            {
                var result = await _userManager.CreateAsync(user, "123456");

                if (!result.Succeeded)
                    continue;
            }

            return RedirectToAction("Index", "Home");
        }
        
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyPage()
        {
            var user = User;
            var result = await _userManager.GetUserAsync(user);

            var model = new UserViewModel((User)result);
            model.Friends = await GetAllFriend(model.User);

            return View("User", model);
        }

        private async Task<List<User>> GetAllFriend(User user)
        {
            var repository = _unitOfWork.GetRepository<Friend>() as FriendsRepository;
            return repository.GetFriendsByUser(user);
        }

        private async Task<List<User>> GetAllFriend()
        {
            var user = User;
            var result = await _userManager.GetUserAsync(user);

            var repository = _unitOfWork.GetRepository<Friend>() as FriendsRepository;
            return repository.GetFriendsByUser((User)result);
        }

        [Route("Edit")]
        [HttpGet]
        public IActionResult Edit()
        {
            var user = User;
            var result = _userManager.GetUserAsync(user);

            var editmodel = _mapper.Map<UserEditViewModel>(result.Result);
            return View("Edit", editmodel);
        }

        [Authorize]
        [Route("Update")]
        [HttpPost]
        public async Task<IActionResult> Update(UserEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                ((User)user).Convert(model);

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction("MyPage", "UserManagement");
                }
                else
                {
                    return RedirectToAction("Edit", "UserManagement");
                }
            }
            else
            {
                ModelState.AddModelError("", "Некорректные данные");
                return View("Edit", model);
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UserList(string search)
        {
            var model = await CreateSearch(search);
            return View("UserList", model);
        }

        private async Task<SearchViewModel> CreateSearch(string search)
        {
            var currentuser = User;
            var result = await _userManager.GetUserAsync(currentuser);

            var list = _userManager.Users.AsEnumerable().ToList();
            if (!string.IsNullOrEmpty(search))
            {
                list = list.Where(x => ((User)x).GetFullName().ToLower().Contains(search.ToLower())).ToList();
            }

            var withfriend = await GetAllFriend();

            var data = new List<UserWithFriendExt>();
            list.ForEach(x =>
            {
                var t = _mapper.Map<UserWithFriendExt>(x);
                t.IsFriendWithCurrent = withfriend.Where(y => y.Id == x.Id || x.Id == result.Id).Count() != 0;
                data.Add(t);
            });

            var model = new SearchViewModel()
            {
                UserList = data
            };

            return model;
        }

        [Route("AddFriend")]
        [HttpPost]
        public async Task<IActionResult> AddFriend(string id)
        {
            var currentuser = User;
            var result = await _userManager.GetUserAsync(currentuser);
            var friend = await _userManager.FindByIdAsync(id);

            var repository = _unitOfWork.GetRepository<Friend>() as FriendsRepository;
            await repository.AddFriend((User)result, (User)friend);

            return RedirectToAction("MyPage", "UserManagement");
        }

        [Route("DeleteFriend")]
        [HttpPost]
        public async Task<IActionResult> DeleteFriend(string id)
        {
            var currentuser = User;
            var result = await _userManager.GetUserAsync(currentuser);
            var friend = await _userManager.FindByIdAsync(id);

            var repository = _unitOfWork.GetRepository<Friend>() as FriendsRepository;
            await repository.DeleteFriend((User)result, (User)friend);

            return RedirectToAction("MyPage", "UserManagement");

        }

        [Route("Chat")]
        [HttpPost]
        public async Task<IActionResult> Chat(string id)
        {
            var model = await GenerateChat(id);
            return View("Chat", model);
        }

        private async Task<ChatViewModel> GenerateChat(string id)
        {
            var currentuser = User;

            var result = await _userManager.GetUserAsync(currentuser);
            var friend = await _userManager.FindByIdAsync(id);

            var repository = _unitOfWork.GetRepository<Message>() as MessageRepository;
            var mess = repository.GetMessages((User)result, (User)friend);

            var model = new ChatViewModel()
            {
                You = (User)result,
                ToWhom = (User)friend,
                History = mess.OrderBy(x => x.Id).ToList(),
            };

            return model;
        }

        [Route("Chat")]
        [HttpGet]
        public async Task<IActionResult> Chat()
        {
            var id = Request.Query["id"];

            var model = await GenerateChat(id);
            return View("Chat", model);
        }

        [Route("NewMessage")]
        [HttpPost]
        public async Task<IActionResult> NewMessage(string id, ChatViewModel chat)
        {
            var currentuser = User;

            var result = await _userManager.GetUserAsync(currentuser);
            var friend = await _userManager.FindByIdAsync(id);

            var repository = _unitOfWork.GetRepository<Message>() as MessageRepository;

            var item = new Message()
            {
                Sender = (User)result,
                Recipient = (User)friend,
                Text = chat.NewMessage.Text,
            };
            await repository.Create(item);

            await _hubContext.Clients.All.SendAsync("NewMessage", ((User)result).FirstName.ToString() + ": " + chat.NewMessage.Text);

            var model = await GenerateChat(id);
            return View("Chat", model);
        }
    }
}
