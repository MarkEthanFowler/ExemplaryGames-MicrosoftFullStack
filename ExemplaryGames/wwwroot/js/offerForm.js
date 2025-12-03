$(function () {
    const form = $("#offerForm");

    if (form.length) {
        form.on("submit", function (e) {
            e.preventDefault();

            var url = form.attr("action");
            var data = form.serialize();

            $.ajax({
                url: url,
                method: "POST",
                data: data,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                success: function (result) {
                    $("#offerResult").text(result.message);

                    // Optionally update dynamic DOM fields
                    if (result.totalOffers !== undefined) {
                        $("#totalOffersValue").text(result.totalOffers);
                    }
                    if (result.maxOffer !== undefined) {
                        $("#maxOfferValue").text(result.maxOffer);
                    }
                },
                error: function () {
                    $("#offerResult").text("An error occurred submitting your offer.");
                }
            });
        });
    }
});