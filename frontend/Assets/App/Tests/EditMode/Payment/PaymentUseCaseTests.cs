using System;
using System.Collections.Generic;
using App.Application.Payment;
using App.Domain.Payment;
using NUnit.Framework;

namespace App.Tests.EditMode.Payment
{
    public sealed class PaymentUseCaseTests
    {
        [Test]
        public void StartCharge_DelegatesToRepository_WithTrimmedBuyer()
        {
            var repo = new FakePaymentRepository();
            var useCase = new PaymentUseCase(repo);

            PaymentChargeStartResult captured = null;
            PaymentApiError error = null;

            useCase.StartCharge(
                ChargePreset.Pages5000,
                "  홍길동  ",
                "  010-1234-5678  ",
                "  buyer@example.com  ",
                result => captured = result,
                err => error = err);

            Assert.IsNull(error);
            Assert.IsNotNull(captured);
            Assert.AreEqual(ChargePreset.Pages5000, repo.LastPreset);
            Assert.AreEqual("홍길동", repo.LastBuyerName);
            Assert.AreEqual("010-1234-5678", repo.LastBuyerTel);
            Assert.AreEqual("buyer@example.com", repo.LastBuyerEmail);
        }

        [Test]
        public void StartCharge_BlankBuyerName_RaisesValidationError()
        {
            var repo = new FakePaymentRepository();
            var useCase = new PaymentUseCase(repo);

            PaymentApiError error = null;
            useCase.StartCharge(
                ChargePreset.Pages1000,
                "   ",
                "010-1234-5678",
                "buyer@example.com",
                _ => Assert.Fail("should not succeed"),
                e => error = e);

            Assert.IsNotNull(error);
            Assert.AreEqual("INVALID_BUYER", error.Code);
            Assert.IsFalse(repo.CreateChargeOrderCalled);
        }

        [Test]
        public void PollOrderStatus_BlankOrderId_RaisesValidationError()
        {
            var repo = new FakePaymentRepository();
            var useCase = new PaymentUseCase(repo);

            PaymentApiError error = null;
            useCase.PollOrderStatus(
                "  ",
                _ => Assert.Fail("should not succeed"),
                e => error = e);

            Assert.IsNotNull(error);
            Assert.AreEqual("INVALID_ORDER_ID", error.Code);
            Assert.IsFalse(repo.GetOrderStatusCalled);
        }

        [Test]
        public void PollOrderStatus_DelegatesToRepository_WithTrimmedOrderId()
        {
            var repo = new FakePaymentRepository();
            var useCase = new PaymentUseCase(repo);

            PaymentPollResult captured = null;
            useCase.PollOrderStatus(
                "  DS-ORDER-1  ",
                r => captured = r,
                _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual("DS-ORDER-1", repo.LastOrderId);
        }

        [Test]
        public void FetchTransactions_NegativeLimit_ClampsToAtLeastOne()
        {
            var repo = new FakePaymentRepository();
            var useCase = new PaymentUseCase(repo);

            useCase.FetchTransactions(
                -50,
                _ => { },
                _ => Assert.Fail("should not error"));

            Assert.AreEqual(1, repo.LastLimit);
        }

        private sealed class FakePaymentRepository : IPaymentRepository
        {
            public ChargePreset LastPreset;
            public string LastBuyerName;
            public string LastBuyerTel;
            public string LastBuyerEmail;
            public string LastOrderId;
            public int LastLimit;
            public bool CreateChargeOrderCalled;
            public bool GetOrderStatusCalled;

            public void CreateChargeOrder(
                ChargePreset preset,
                string buyerName,
                string buyerTel,
                string buyerEmail,
                Action<PaymentChargeStartResult> onSuccess,
                Action<PaymentApiError> onError)
            {
                CreateChargeOrderCalled = true;
                LastPreset = preset;
                LastBuyerName = buyerName;
                LastBuyerTel = buyerTel;
                LastBuyerEmail = buyerEmail;
                var order = new PaymentOrder(
                    "DS-ORDER-1",
                    "DotShelf 5000P 충전",
                    ChargePresets.PagesOf(preset),
                    ChargePresets.PagesOf(preset),
                    PaymentStatus.Ready,
                    "/v1/payments/inicis/request/DS-ORDER-1",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(30));
                onSuccess?.Invoke(new PaymentChargeStartResult(order, "https://api.example/v1/payments/inicis/request/DS-ORDER-1"));
            }

            public void GetOrderStatus(
                string orderId,
                Action<PaymentPollResult> onSuccess,
                Action<PaymentApiError> onError)
            {
                GetOrderStatusCalled = true;
                LastOrderId = orderId;
                onSuccess?.Invoke(new PaymentPollResult(orderId, PaymentStatus.Ready, null));
            }

            public void GetTransactions(
                int limit,
                Action<PaymentTransactionsResult> onSuccess,
                Action<PaymentApiError> onError)
            {
                LastLimit = limit;
                onSuccess?.Invoke(new PaymentTransactionsResult(new List<PageTransactionView>()));
            }
        }
    }
}
