const hubConnection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .build();

// получение сообщения от сервера
hubConnection.on('NewMessage', function (message) {

    let elem = document.createElement("span");
    elem.appendChild(document.createTextNode(message));

    document.getElementById("notify").appendChild(elem);

    document.getElementById("notify").appendChild(document.createElement("br"));

});
hubConnection.start();