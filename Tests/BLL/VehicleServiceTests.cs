using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class VehicleServiceTests
    {
        private readonly Mock<IPhotoService> _photoService = new();
        private readonly Mock<IEmailService> _emailService = new();

        private AutoWashDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AutoWashDbContext(options);
        }

        private VehicleService CreateService(AutoWashDbContext context)
        {
            return new VehicleService(
                context,
                _photoService.Object,
                _emailService.Object);
        }

        private Tier CreateTier()
        {
            return new Tier
            {
                TierId = 1,
                TierName = "Gold",
                BookingWindowDays = 7,
                MinAccumulatedPoints = 0,
                PointMultiplier = 1
            };
        }

        private User CreateUser(int id = 1)
        {
            var tier = CreateTier();

            var user = new User
            {
                UserId = id,
                PhoneNumber = "0900000000",
                Email = "user@test.com",
                PasswordHash = "hash",
                Role = "Customer",
                Status = "Active"
            };

            user.CustomerProfile = new CustomerProfile
            {
                ProfileId = id,
                UserId = id,
                User = user,
                FullName = "John Doe",
                Tier = tier,
                TierId = tier.TierId
            };

            return user;
        }

        private VehicleType CreateVehicleType(
            int id = 1,
            string name = "Sedan")
        {
            return new VehicleType
            {
                Id = id,
                Name = name,
                Description = ""
            };
        }

        private CarModel CreateCarModel(
            int id = 1,
            int vehicleTypeId = 1)
        {
            return new CarModel
            {
                Id = id,
                Brand = "Honda",
                Name = "Civic",
                VehicleTypeId = vehicleTypeId,
                IsActive = true,
                Status = "Approved"
            };
        }

        private Vehicle CreateVehicle(
            User user,
            VehicleType type,
            CarModel model,
            string plate = "51A12345")
        {
            return new Vehicle
            {
                LicensePlate = plate,
                User = user,
                UserId = user.UserId,
                VehicleType = type,
                VehicleTypeId = type.Id,
                CarModelEntity = model,
                CarModelId = model.Id
            };
        }

        #region GetMyVehiclesAsync

        [Fact]
        public async Task GetMyVehiclesAsync_ShouldReturnOnlyUserVehicles()
        {
            var context = CreateContext();

            var user = CreateUser();

            var type = CreateVehicleType();

            var model = CreateCarModel();

            context.Users.Add(user);
            context.Tiers.Add(user.CustomerProfile.Tier);
            context.CustomerProfiles.Add(user.CustomerProfile);

            context.VehicleTypes.Add(type);
            context.CarModels.Add(model);

            context.Vehicles.Add(CreateVehicle(user, type, model));

            context.Vehicles.Add(new Vehicle
            {
                LicensePlate = "OTHER",
                UserId = 99,
                VehicleType = type,
                VehicleTypeId = 1,
                CarModelEntity = model,
                CarModelId = 1
            });

            context.Vehicles.Add(new Vehicle
            {
                LicensePlate = "DELETED",
                UserId = 1,
                VehicleType = type,
                VehicleTypeId = 1,
                CarModelEntity = model,
                CarModelId = 1,
                IsDeleted = true
            });

            await context.SaveChangesAsync();

            var service = CreateService(context);

            var result = await service.GetMyVehiclesAsync(1);

            Assert.Single(result);

            Assert.Equal("51A12345",
                result.First().LicensePlate);
        }

        [Fact]
        public async Task GetMyVehiclesAsync_ShouldReturnEmpty_WhenUserHasNoVehicle()
        {
            var context = CreateContext();

            var service = CreateService(context);

            var result = await service.GetMyVehiclesAsync(1);

            Assert.Empty(result);
        }

        #endregion

        #region AddVehicleAsync

        [Fact]
        public async Task AddVehicleAsync_ShouldAddVehicle()
        {
            var context = CreateContext();

            context.VehicleTypes.Add(CreateVehicleType());

            context.CarModels.Add(CreateCarModel());

            await context.SaveChangesAsync();

            var service = CreateService(context);

            var dto = new CreateVehicleDTO
            {
                LicensePlate = "51A-123.45",
                CarModelId = 1
            };

            var result = await service.AddVehicleAsync(1, dto);

            Assert.True(result);

            Assert.Single(context.Vehicles);

            Assert.Equal("51A12345",
                context.Vehicles.First().LicensePlate);
        }

        [Fact]
        public async Task AddVehicleAsync_ShouldThrow_WhenVehicleTypeMissing()
        {
            var context = CreateContext();

            var service = CreateService(context);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                service.AddVehicleAsync(1,
                    new CreateVehicleDTO
                    {
                        LicensePlate = "51A12345"
                    }));
        }

        [Fact]
        public async Task AddVehicleAsync_ShouldThrow_WhenCarModelNotFound()
        {
            var context = CreateContext();

            context.VehicleTypes.Add(CreateVehicleType());

            await context.SaveChangesAsync();

            var service = CreateService(context);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                service.AddVehicleAsync(1,
                    new CreateVehicleDTO
                    {
                        LicensePlate = "51A12345",
                        CarModelId = 99
                    }));
        }

        [Fact]
        public async Task AddVehicleAsync_ShouldThrow_WhenVehicleTypeInvalid()
        {
            var context = CreateContext();

            context.CarModels.Add(CreateCarModel());

            await context.SaveChangesAsync();

            var service = CreateService(context);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                service.AddVehicleAsync(1,
                    new CreateVehicleDTO
                    {
                        LicensePlate = "51A12345",
                        VehicleTypeId = 99,
                        CarModel = "Unknown"
                    }));
        }

        [Fact]
        public async Task AddVehicleAsync_ShouldThrow_WhenVehicleLimitReached()
        {
            var context = CreateContext();

            var type = CreateVehicleType();

            var model = CreateCarModel();

            context.VehicleTypes.Add(type);

            context.CarModels.Add(model);

            for (int i = 0; i < 5; i++)
            {
                context.Vehicles.Add(new Vehicle
                {
                    LicensePlate = $"51A{i}",
                    UserId = 1,
                    VehicleTypeId = 1,
                    VehicleType = type,
                    CarModelId = 1,
                    CarModelEntity = model
                });
            }

            await context.SaveChangesAsync();

            var service = CreateService(context);

            await Assert.ThrowsAsync<BadRequestException>(() =>
                service.AddVehicleAsync(1,
                    new CreateVehicleDTO
                    {
                        LicensePlate = "NEW123",
                        CarModelId = 1
                    }));
        }
        #endregion

        #region AddVehicleAsync - more branches

        [Fact]
        public async Task AddVehicleAsync_OtherType_NoPhoto_ThrowsBadRequestException()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            context.VehicleTypes.Add(otherType);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var dto = new CreateVehicleDTO { LicensePlate = "51A99911", VehicleTypeId = 1, CarModel = "Custom Truck" };

            await Assert.ThrowsAsync<BadRequestException>(() => service.AddVehicleAsync(1, dto));
        }

        [Fact]
        public async Task AddVehicleAsync_OtherType_NoNote_ThrowsBadRequestException()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            context.VehicleTypes.Add(otherType);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var dto = new CreateVehicleDTO { LicensePlate = "51A99912", VehicleTypeId = 1, CarModel = "Custom Truck", RegistrationPhotoUrl = "http://photo.jpg" };

            await Assert.ThrowsAsync<BadRequestException>(() => service.AddVehicleAsync(1, dto));
        }

        [Fact]
        public async Task AddVehicleAsync_ReactivatesSoftDeletedVehicleWithSamePlate()
        {
            var context = CreateContext();
            var type = CreateVehicleType();
            var model = CreateCarModel();
            context.VehicleTypes.Add(type);
            context.CarModels.Add(model);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51A55555", UserId = 99, VehicleTypeId = 1, VehicleType = type, CarModelId = 1, CarModelEntity = model, IsDeleted = true });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new CreateVehicleDTO { LicensePlate = "51A-555.55", CarModelId = 1 };

            var result = await service.AddVehicleAsync(1, dto);

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51A55555");
            Assert.False(vehicle.IsDeleted);
            Assert.Equal(1, vehicle.UserId);
        }

        [Fact]
        public async Task AddVehicleAsync_DuplicateActivePlate_ThrowsBadRequestException()
        {
            var context = CreateContext();
            var type = CreateVehicleType();
            var model = CreateCarModel();
            context.VehicleTypes.Add(type);
            context.CarModels.Add(model);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51A66666", UserId = 2, VehicleTypeId = 1, VehicleType = type, CarModelId = 1, CarModelEntity = model, IsDeleted = false });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new CreateVehicleDTO { LicensePlate = "51A66666", CarModelId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => service.AddVehicleAsync(1, dto));
        }

        #endregion

        #region GetOtherVehiclesAsync

        [Fact]
        public async Task GetOtherVehiclesAsync_ReturnsOnlyOtherTypeVehicles()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            var sedanType = new VehicleType { Id = 2, Name = "Sedan", Description = "" };
            context.VehicleTypes.AddRange(otherType, sedanType);
            var user = CreateUser();
            context.Users.Add(user);
            context.Tiers.Add(user.CustomerProfile.Tier);
            context.CustomerProfiles.Add(user.CustomerProfile);
            await context.SaveChangesAsync();

            context.Vehicles.AddRange(
                new Vehicle { LicensePlate = "51B11111", UserId = 1, VehicleTypeId = 1, VehicleType = otherType, IsDeleted = false },
                new Vehicle { LicensePlate = "51B22222", UserId = 1, VehicleTypeId = 2, VehicleType = sedanType, IsDeleted = false }
            );
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.GetOtherVehiclesAsync();

            Assert.Single(result);
            Assert.Equal("51B11111", result[0].LicensePlate);
        }

        #endregion

        #region UpdateVehicleAsync

        [Fact]
        public async Task UpdateVehicleAsync_NotFoundOrNotOwned_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            var dto = new UpdateVehicleDTO { CarModelId = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateVehicleAsync(1, "51C11111", dto));
        }

        [Fact]
        public async Task UpdateVehicleAsync_Valid_UpdatesFields()
        {
            var context = CreateContext();
            var type = CreateVehicleType();
            var model = CreateCarModel();
            context.VehicleTypes.Add(type);
            context.CarModels.Add(model);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51C22222", UserId = 1, VehicleTypeId = 1, VehicleType = type, CarModelId = 1, CarModelEntity = model, UserNote = "old note" });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new UpdateVehicleDTO { CarModelId = 1, UserNote = "new note" };

            var result = await service.UpdateVehicleAsync(1, "51C22222", dto);

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51C22222");
            Assert.Equal("new note", vehicle.UserNote);
        }

        #endregion

        #region DeleteVehicleAsync

        [Fact]
        public async Task DeleteVehicleAsync_NotFoundOrNotOwned_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteVehicleAsync(1, "51D11111"));
        }

        [Fact]
        public async Task DeleteVehicleAsync_Valid_SoftDeletes()
        {
            var context = CreateContext();
            var type = CreateVehicleType();
            context.VehicleTypes.Add(type);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51D22222", UserId = 1, VehicleTypeId = 1, VehicleType = type });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.DeleteVehicleAsync(1, "51D22222");

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51D22222");
            Assert.True(vehicle.IsDeleted);
        }

        #endregion

        #region RecognizeVehicleAsync

        [Fact]
        public async Task RecognizeVehicleAsync_NotRegistered_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            await Assert.ThrowsAsync<NotFoundException>(() => service.RecognizeVehicleAsync("51E11111"));
        }

        [Fact]
        public async Task RecognizeVehicleAsync_NoActiveBooking_ReturnsHasActiveBookingFalse()
        {
            var context = CreateContext();
            var user = CreateUser();
            var type = CreateVehicleType();
            context.Users.Add(user);
            context.Tiers.Add(user.CustomerProfile.Tier);
            context.CustomerProfiles.Add(user.CustomerProfile);
            context.VehicleTypes.Add(type);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51E22222", UserId = 1, User = user, VehicleTypeId = 1, VehicleType = type });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.RecognizeVehicleAsync("51E22222");

            Assert.False(result.HasActiveBooking);
            Assert.Equal("John Doe", result.OwnerName);
        }

        [Fact]
        public async Task RecognizeVehicleAsync_HasActiveBookingToday_ReturnsTrue()
        {
            var context = CreateContext();
            var user = CreateUser();
            var type = CreateVehicleType();
            context.Users.Add(user);
            context.Tiers.Add(user.CustomerProfile.Tier);
            context.CustomerProfiles.Add(user.CustomerProfile);
            context.VehicleTypes.Add(type);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51E33333", UserId = 1, User = user, VehicleTypeId = 1, VehicleType = type });
            context.Bookings.Add(new Booking { LicensePlate = "51E33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.RecognizeVehicleAsync("51E33333");

            Assert.True(result.HasActiveBooking);
        }

        #endregion

        #region ApproveNewVehicleTypeAsync / RejectNewVehicleTypeAsync / UpdateVehicleTypeByAdminAsync

        [Fact]
        public async Task ApproveNewVehicleTypeAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            var dto = new ApproveVehicleTypeRequestDTO { CustomizedTypeName = "Pickup" };

            await Assert.ThrowsAsync<NotFoundException>(() => service.ApproveNewVehicleTypeAsync("51F11111", dto));
        }

        [Fact]
        public async Task ApproveNewVehicleTypeAsync_NotOtherType_ThrowsBadRequestException()
        {
            var context = CreateContext();
            var type = CreateVehicleType(name: "Sedan");
            context.VehicleTypes.Add(type);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51F22222", UserId = 1, VehicleTypeId = 1, VehicleType = type });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new ApproveVehicleTypeRequestDTO { CustomizedTypeName = "Pickup" };

            await Assert.ThrowsAsync<BadRequestException>(() => service.ApproveNewVehicleTypeAsync("51F22222", dto));
        }

        [Fact]
        public async Task ApproveNewVehicleTypeAsync_ExistingTypeName_ReusesIt()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            var pickupType = new VehicleType { Id = 2, Name = "Pickup", Description = "" };
            context.VehicleTypes.AddRange(otherType, pickupType);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51F33333", UserId = 1, VehicleTypeId = 1, VehicleType = otherType });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new ApproveVehicleTypeRequestDTO { CustomizedTypeName = "Pickup" };

            var result = await service.ApproveNewVehicleTypeAsync("51F33333", dto);

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51F33333");
            Assert.Equal(pickupType.Id, vehicle.VehicleTypeId);

            var typeCount = await context.VehicleTypes.CountAsync(t => t.Name == "Pickup");
            Assert.Equal(1, typeCount); // not duplicated
        }

        [Fact]
        public async Task ApproveNewVehicleTypeAsync_NewTypeName_CreatesNewType()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            context.VehicleTypes.Add(otherType);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51F44444", UserId = 1, VehicleTypeId = 1, VehicleType = otherType });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var dto = new ApproveVehicleTypeRequestDTO { CustomizedTypeName = "Minivan" };

            var result = await service.ApproveNewVehicleTypeAsync("51F44444", dto);

            Assert.True(result);
            var newType = await context.VehicleTypes.FirstOrDefaultAsync(t => t.Name == "Minivan");
            Assert.NotNull(newType);
        }

        [Fact]
        public async Task RejectNewVehicleTypeAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            await Assert.ThrowsAsync<NotFoundException>(() => service.RejectNewVehicleTypeAsync("51G11111"));
        }

        [Fact]
        public async Task RejectNewVehicleTypeAsync_Valid_SoftDeletes()
        {
            var context = CreateContext();
            var otherType = new VehicleType { Id = 1, Name = "Other", Description = "" };
            context.VehicleTypes.Add(otherType);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51G22222", UserId = 1, VehicleTypeId = 1, VehicleType = otherType });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.RejectNewVehicleTypeAsync("51G22222");

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51G22222");
            Assert.True(vehicle.IsDeleted);
        }

        [Fact]
        public async Task UpdateVehicleTypeByAdminAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var context = CreateContext();
            var service = CreateService(context);

            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateVehicleTypeByAdminAsync("51H11111", 1));
        }

        [Fact]
        public async Task UpdateVehicleTypeByAdminAsync_InvalidType_ThrowsBadRequestException()
        {
            var context = CreateContext();
            var type = CreateVehicleType();
            context.VehicleTypes.Add(type);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51H22222", UserId = 1, VehicleTypeId = 1, VehicleType = type });
            await context.SaveChangesAsync();

            var service = CreateService(context);

            await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateVehicleTypeByAdminAsync("51H22222", 999));
        }

        [Fact]
        public async Task UpdateVehicleTypeByAdminAsync_Valid_UpdatesType()
        {
            var context = CreateContext();
            var type1 = new VehicleType { Id = 1, Name = "Sedan", Description = "" };
            var type2 = new VehicleType { Id = 2, Name = "SUV", Description = "" };
            context.VehicleTypes.AddRange(type1, type2);
            context.Vehicles.Add(new Vehicle { LicensePlate = "51H33333", UserId = 1, VehicleTypeId = 1, VehicleType = type1 });
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var result = await service.UpdateVehicleTypeByAdminAsync("51H33333", 2);

            Assert.True(result);
            var vehicle = await context.Vehicles.FirstAsync(v => v.LicensePlate == "51H33333");
            Assert.Equal(2, vehicle.VehicleTypeId);
        }

        #endregion
    }
}